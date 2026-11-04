using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using KiHan.Network;
using Managers;

/// <summary>
/// 帧同步驱动 (客户端定时器驱动)
///
/// 客户端自己的定时器按真实时间以 1/30 秒步进 (累加器，与渲染帧率无关)，每一步：
/// 1. 采样本机输入发往服务器，输入标记目标帧号 = 当前帧 + 1 + InputDelayFrames (输入延迟用于吸收网络往返)
/// 2. 执行下一个确认帧 (服务器合成的全体玩家输入)；未到达时本步等待 (不执行、不预测，逻辑只吃确认帧)
/// 3. 追帧：已到达未执行的确认帧超过 MaxBufferedFrames 时同一步多执行几帧，把积压的延迟追回来
///
/// 服务器只负责把各玩家同一帧号的输入合成确认帧下发 (2005)，不再驱动客户端节奏；
/// 单机训练场由 VirtualNetworkManager 收到输入后立即回送确认帧 (输入延迟 0)。
/// 单次 Update 最多走 MaxStepsPerUpdate 步，落后更多 (断点 / 长时间卡顿) 时丢弃积累的时间，不连续快进
/// </summary>
public class LockstepManager : UnitySingleton<LockstepManager>
{
    /// <summary>
    /// 联机默认输入延迟 (逻辑帧)：约 66ms，覆盖常见网络往返
    /// </summary>
    public const int OnlineInputDelayFrames = 2;

    /// <summary>
    /// 允许积压的确认帧数，超过后追帧
    /// </summary>
    private const int MaxBufferedFrames = 2;

    /// <summary>
    /// 一步内最多执行的确认帧数 (含本步正常的 1 帧)
    /// </summary>
    private const int MaxFramesPerStep = 3;

    /// <summary>
    /// 一次 Update 最多走的步数
    /// </summary>
    private const int MaxStepsPerUpdate = 4;

    private static readonly double StepSeconds = 1.0 / GameConfig.LOGIC_FPS;

    private uint _currFrameId;
    private uint _nextSendFrameId = 1;
    private double _accumulator;
    private bool _gameStarted;

    /// <summary>
    /// 定时器时钟 (开局起累计的真实时间) 与最近一次执行逻辑帧的名义时刻 (该步应发生的时刻 = 本步结束时的时钟 - 剩余累加量)
    /// </summary>
    private double _clock;
    private double _lastTickClock;

    private readonly SortedList<uint, RoomFrame> _serverFrames = new SortedList<uint, RoomFrame>();

    public Action<RoomFrame> OnExecuteFrame;
    public Action OnGameStart;
    public byte MyGameId { get; private set; }

    /// <summary>
    /// 本机输入延迟 (逻辑帧)，由 Init 指定
    /// </summary>
    public int InputDelayFrames { get; private set; }

    /// <summary>
    /// 已执行的最后一个逻辑帧
    /// </summary>
    public uint CurrentFrameId => _currFrameId;

    /// <summary>
    /// 已到达、尚未执行的确认帧数
    /// </summary>
    public int BufferedFrames => _serverFrames.Count;

    /// <summary>
    /// 距最近一次逻辑帧的进度 [0, 1] (按定时器相位，与渲染帧率无关)：表现层在上一帧与当前帧快照之间插值用
    /// 逻辑帧在渲染帧中的执行时机有抖动，不能用 Time.time 记录执行时刻；等待确认帧时停在 1 (保持当前帧快照，不回退)
    /// </summary>
    public float TickAlpha => _gameStarted ? Mathf.Clamp01((float)((_clock - _lastTickClock) / StepSeconds)) : 1f;

    /// <summary>
    /// 因确认帧未到达而等待的步数 (开局起累计，用于观察网络状况)
    /// </summary>
    public int StalledSteps { get; private set; }

    private INetworkChannel _netChannel;

    /// <summary>
    /// 本机输入与上报缓冲 (复用；网络层发送时会拷贝内容)
    /// </summary>
    private readonly InputFrame _localInput = new InputFrame();
    private readonly byte[] _sendBuffer = new byte[InputFrame.DataLength];

    public void Init(INetworkChannel channel, int inputDelayFrames)
    {
        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived -= HandleNetworkMessage;
        }

        _netChannel = channel;
        InputDelayFrames = Mathf.Max(0, inputDelayFrames);

        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived += HandleNetworkMessage;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived -= HandleNetworkMessage;
        }
    }

    public void StartGame()
    {
        Debug.Log($"[Lockstep] 帧同步启动 (客户端定时器驱动，输入延迟 {InputDelayFrames} 帧)");
        _gameStarted = true;
        _currFrameId = 0;
        _nextSendFrameId = 1;
        _accumulator = 0;
        _clock = 0;
        _lastTickClock = 0;
        StalledSteps = 0;
        _serverFrames.Clear();
        OnGameStart?.Invoke();
    }

    public void StopGame()
    {
        Debug.Log("[Lockstep] 停止帧同步");
        _gameStarted = false;
        _serverFrames.Clear();
        _currFrameId = 0;
        _nextSendFrameId = 1;
        _accumulator = 0;

        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived -= HandleNetworkMessage;
            _netChannel = null;
        }
    }

    private void Update()
    {
        if (!_gameStarted) return;

        double dt = Time.unscaledDeltaTime;
        _clock += dt;
        _accumulator += dt;
        int steps = 0;
        bool ticked = false;
        while (_gameStarted && _accumulator >= StepSeconds && steps < MaxStepsPerUpdate)
        {
            _accumulator -= StepSeconds;
            if (Step())
            {
                ticked = true;
                _lastTickClock = _clock - _accumulator;
            }
            steps++;
        }
        // 落后超过一步以上 (断点 / 长时间卡顿)：丢弃积累的时间，从现在重新计时
        if (_accumulator >= StepSeconds)
        {
            _accumulator = 0;
            if (ticked) _lastTickClock = _clock;
        }
    }

    /// <summary>
    /// 定时器的一步：上报输入 → 执行下一个确认帧 → 积压过多时追帧；返回是否执行了逻辑帧
    /// </summary>
    private bool Step()
    {
        SendInputs();

        if (!TryExecuteNext())
        {
            StalledSteps++;
            return false;
        }

        int executed = 1;
        while (executed < MaxFramesPerStep && _serverFrames.Count > MaxBufferedFrames && TryExecuteNext())
        {
            executed++;
        }
        return true;
    }

    /// <summary>
    /// 输入领先执行帧 InputDelayFrames 帧：补齐到目标帧号 (开局一次发出延迟窗口内的帧；追帧后补发跳过的帧)
    /// </summary>
    private void SendInputs()
    {
        uint target = _currFrameId + 1 + (uint)InputDelayFrames;
        while (_nextSendFrameId <= target && _gameStarted)
        {
            SendInputToServer(CaptureLocalInput(_nextSendFrameId));
            _nextSendFrameId++;
        }
    }

    private bool TryExecuteNext()
    {
        uint next = _currFrameId + 1;
        if (!_serverFrames.TryGetValue(next, out RoomFrame frame)) return false;

        _serverFrames.Remove(next);
        _currFrameId = next;
        OnExecuteFrame?.Invoke(frame);
        return true;
    }

    private InputFrame CaptureLocalInput(uint frameId)
    {
        InputFrame input = _localInput;
        input.FrameId = frameId;

        // 使用统筹后的 InputManager 获取 8 方向编码和按键掩码
        input.JoyStickDir = InputManager.Instance.GetJoystickDir();
        input.Buttons = InputManager.Instance.GetCombinedButtons();

        return input;
    }

    private void SendInputToServer(InputFrame input)
    {
        // 按照新协议：raw_input 是 6 字节: [FrameId(4,大端)][Joystick(1)][Buttons(1)]
        byte[] raw = _sendBuffer;
        raw[0] = (byte)((input.FrameId >> 24) & 0xFF);
        raw[1] = (byte)((input.FrameId >> 16) & 0xFF);
        raw[2] = (byte)((input.FrameId >> 8) & 0xFF);
        raw[3] = (byte)(input.FrameId & 0xFF);
        raw[4] = input.JoyStickDir;
        raw[5] = (byte)input.Buttons;

        if (_netChannel != null && _netChannel.IsConnected)
        {
            _netChannel.SendMsg(2004, raw);
        }
    }

    /// <summary>
    /// 本地服务器 (单机) 直接递交确认帧；已执行或重复的帧忽略
    /// </summary>
    public void InjectLocalFrame(RoomFrame frame)
    {
        AddServerFrame(frame);
    }

    private void AddServerFrame(RoomFrame frame)
    {
        if (frame == null || frame.FrameId <= _currFrameId || _serverFrames.ContainsKey(frame.FrameId)) return;
        _serverFrames.Add(frame.FrameId, frame);
    }

    private void HandleNetworkMessage(ushort cmdId, byte[] payload)
    {
        if (cmdId == 2005) // RoomFrameUpdate
        {
            var update = RoomFrameUpdate.Deserialize(payload);
            if (update != null) AddServerFrame(update.frame);
        }
        else if (cmdId == 2003) // GameStartNtf
        {
            Debug.Log($"[Lockstep] GameStartNtf");
            StartGame();
        }
        else if (cmdId == 2001) // EnterRoomRsp
        {
            var rsp = EnterRoomRsp.Deserialize(payload);
            if (rsp.err_code == 0)
            {
                MyGameId = (byte)rsp.my_game_id;

                // TODO: 可以在这里提取 snapshot.player_list_json，初始化玩家实体
                Debug.Log($"[Lockstep] EnterRoomRsp: MyGameId={MyGameId}, Snapshot={rsp.snapshot?.player_list_json}");

                // 进房成功后，发送 2002 PlayerReadyReq
                var readyReq = new PlayerReadyReq { room_id = rsp.snapshot != null ? rsp.snapshot.room_id : 1 };
                _netChannel?.SendMsg(2002, readyReq.Serialize());
            }
        }
        else if (cmdId == 2002) // PlayerReadyRsp
        {
            Debug.Log($"[Lockstep] PlayerReadyRsp");
        }
        else if (cmdId == 2007) // GameOverNtf
        {
            var ntf = GameOverNtf.Deserialize(payload);
            Debug.Log($"[Lockstep] GameOverNtf received, winner is {ntf.winner_uid}");
            StopGame();

            // 自动回到大厅
            GameFlow.Instance.ExitBattle();
        }
    }
}
