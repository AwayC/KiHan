using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using KiHan.Network;
using Managers;
using System.Security.Cryptography;

public class LockstepManager : UnitySingleton<LockstepManager>
{
    public static float LOGIC_INTERVAL => GameConfig.LOGIC_TICK_TIME; 

    private uint _currFrameId = 0;   
    private uint _nextSendFrameId = 1; 
    
    private bool _gameStarted = false;

    private SortedList<uint, RoomFrame> _serverFrames = new SortedList<uint, RoomFrame>();

    public Action<RoomFrame> OnExecuteFrame;  
    public Action OnGameStart;
    public byte MyGameId { get; private set; }

    private INetworkChannel _netChannel;

    public void Init(INetworkChannel channel)
    {
        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived -= HandleNetworkMessage;
        }

        _netChannel = channel;

        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived += HandleNetworkMessage;
        }
    }

    private void Start()
    {
        // 自动寻找默认通道 (兼容老逻辑)
        //if (GatewayManager.Instance != null)
        //{
        //    Init(GatewayManager.Instance);
        //}
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
        Debug.Log("[Lockstep] 严格帧同步启动");
        _gameStarted = true;
        _currFrameId = 0;
        _nextSendFrameId = 1;
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
        
        if (_netChannel != null)
        {
            _netChannel.OnMessageReceived -= HandleNetworkMessage;
            _netChannel = null;
        }
    }

    private void Update()
    {
        if (!_gameStarted) return;

        ExecuteServerFrames();

        // 这里不要修改
        SendInputStep();
    }

    private void SendInputStep()
    {
        InputFrame localInput = CaptureLocalInput(_nextSendFrameId);
        SendInputToServer(localInput);
        _nextSendFrameId++;
    }

    private void ExecuteServerFrames()
    {
        while (_serverFrames.ContainsKey(_currFrameId + 1))
        {
            uint targetFrameId = _currFrameId + 1;
            RoomFrame serverFrame = _serverFrames[targetFrameId];
            OnExecuteFrame?.Invoke(serverFrame);
            _currFrameId = targetFrameId;
            _serverFrames.Remove(targetFrameId);
        }
    }

    private InputFrame CaptureLocalInput(uint frameId)
    {
        InputFrame input = new InputFrame { FrameId = frameId };
        
        // 使用统筹后的 InputManager 获取角度和按键掩码
        input.JoyStickAngle = InputManager.Instance.GetJoystickAngle();
        input.Buttons = InputManager.Instance.GetCombinedButtons();
        
        return input;
    }

    private void SendInputToServer(InputFrame input)
    {
        // 按照新协议：raw_input 是 6 字节: [FrameId(4,大端)][Joystick(1)][Buttons(1)]
        byte[] raw = new byte[6];
        raw[0] = (byte)((input.FrameId >> 24) & 0xFF);
        raw[1] = (byte)((input.FrameId >> 16) & 0xFF);
        raw[2] = (byte)((input.FrameId >> 8) & 0xFF);
        raw[3] = (byte)(input.FrameId & 0xFF);
        raw[4] = input.JoyStickAngle;
        raw[5] = (byte)input.Buttons;

        // 封装为 protobuf 的 PlayerFrameInput 对象
        //var req = new PlayerFrameInput { raw_input = raw };

        if (_netChannel != null && _netChannel.IsConnected)
        {
            _netChannel.SendMsg(2004, raw);
        }
    }

    public void InjectLocalFrame(RoomFrame frame)
    {
        if (frame != null && !_serverFrames.ContainsKey(frame.FrameId))
        {
            _serverFrames.Add(frame.FrameId, frame);
        }
    }

    private void HandleNetworkMessage(ushort cmdId, byte[] payload)
    {
        if (cmdId == 2005) // RoomFrameUpdate
        {
            var update = RoomFrameUpdate.Deserialize(payload);
            if (update != null && update.frame != null)
            {
                if (!_serverFrames.ContainsKey(update.frame.FrameId))
                {
                    _serverFrames.Add(update.frame.FrameId, update.frame);
                }
            }
        }
        else if (cmdId == 2003) // GameStartNtf
        {
            Debug.Log($"[Lockstep] GameStartNtf");
            var ntf = GameStartNtf.Deserialize(payload);
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
            GameApp.Instance.ExitGame();
        }
    }
}
