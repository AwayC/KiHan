using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using kcp2k;
using System.ComponentModel.DataAnnotations;

public class LockstepManager : UnitySingleton<LockstepManager>
{
    [Header("Settings")]
    public const float LOGIC_INTERVAL = 0.066f; 
    public int maxCatchUpPerFrame = 20; // 追帧上限

    // 帧 ID
    private uint _receivedFrameId = 0; // 本地收到的最新帧
    private uint _currFrameId = 0;   // 本地已经更新的最新帧
    
    private float _timer = 0;
    private bool _gameStarted = false;

    // 缓冲区
    private SortedList<uint, RoomFrame> _RecievedFrames = new SortedList<uint, RoomFrame>();
    private Dictionary<uint, InputFrame> _localInputHistory = new Dictionary<uint, InputFrame>();

    // 逻辑层接口
    public Action<RoomFrame> OnExecuteFrame;  // 执行一帧逻辑
    public Action<uint> OnJumpFrame;      // 确认一帧（服务器已收到并处理）
    public Action<uint> OnSaveSnapshot;       // 保存当前帧快照
    public Action OnRollbackFrame;       // 回滚到某帧快照

    public void StartGame()
    {
        _gameStarted = true;
        _receivedFrameId = 0;
        _currFrameId = 0;
        _serverFrames.Clear();
        _localInputHistory.Clear();
        OnSaveSnapshot?.Invoke(0); // 保存初始状态
    }

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnDataReceived += HandleNetworkData;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnDataReceived -= HandleNetworkData;
        }
    }

    private void Update()
    {
        if (!_gameStarted) return;

        OnRollbackFrame?.Invoke(); // 1. 回滚到前一帧的状态

        while(_receivedFrameId + 1)

        // 2. 本地驱动（预测
        _timer += Time.deltaTime;
        if (_timer >= LOGIC_INTERVAL)
        {
            _timer -= LOGIC_INTERVAL;
            PredictNextFrame();
        }
    }

    // 预测下一帧
    private void PredictNextFrame()
    {
        _currFrameId++;
        
        // 采集本地输入
        InputFrame localInput = CaptureLocalInput(_currFrameId);
        _localInputHistory[_currFrameId] = localInput;

        // 构造一个临时的预测帧
        GameFrame predictFrame = new GameFrame
        {
            FrameId = _currFrameId,
            AllPlayerInputs = new InputFrame[] { localInput } 
            // 注意：这里只用了本地输入进行预测，其他玩家可能保持上一帧状态或空输入
        };

        // 执行预测逻辑
        OnExecuteFrame?.Invoke(predictFrame);
        
        // 保存快照（为了后续能回滚到这一帧）
        OnSaveSnapshot?.Invoke(_currFrameId);

        // 发送给服务器
        SendInputToServer(localInput);
    }

    private void CheckServerFramesAndRollback()
    {
        uint catchUpToFrameId = Min(_receivedFrameId, _currFrameId + (uint)maxCatchUpPerFrame);
        OnRollbackFrame?.Invoke(); // 回滚到当前帧的状态
        // while(_currFrameId < catchUpToFrameId)
    }

    private void ReSimulate()
    {
        // 如果预测跑在确认的前面，需要重新执行中间的预测帧
        uint tempFrameId = _confirmedFrameId;
        while (tempFrameId < _currFrameId)
        {
            tempFrameId++;
            
            // 使用历史保存的本地输入，加上对其他玩家的预测（通常是空或重复）
            GameFrame redoFrame = new GameFrame
            {
                FrameId = tempFrameId,
                AllPlayerInputs = new InputFrame[] { _localInputHistory[tempFrameId] }
            };

            OnExecuteFrame?.Invoke(redoFrame);
            OnSaveSnapshot?.Invoke(tempFrameId);
        }
    }

    private InputFrame CaptureLocalInput(uint frameId)
    {
        InputFrame input = new InputFrame { FrameId = frameId };
        
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        if (horizontal != 0 || vertical != 0)
        {
            float angle = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;
            input.JoyStickAngle = (byte)(angle * 255 / 360);
        }
        else input.JoyStickAngle = 255;

        ButtonMask buttons = ButtonMask.None;
        if (Input.GetKey(KeyCode.J)) buttons |= ButtonMask.Attack;
        if (Input.GetKey(KeyCode.U)) buttons |= ButtonMask.Skill1;
        if (Input.GetKey(KeyCode.I)) buttons |= ButtonMask.Skill2;
        if (Input.GetKey(KeyCode.O)) buttons |= ButtonMask.Ultimate;
        input.Buttons = buttons;

        return input;
    }

    private void SendInputToServer(InputFrame input)
    {
        byte[] data = new byte[7]; 
        data[0] = (byte)ClientOpCode.PlayerFrameInput;
        input.Serialize(data, 1);
        NetworkManager.Instance.Send(data, KcpChannel.Reliable);
    }

    private void HandleNetworkData(ArraySegment<byte> data, KcpChannel channel)
    {
        if (data.Count < 1) return;
        byte opCode = data.Array[data.Offset];

        if (opCode == (byte)ServerOpCode.GameFrameUpdate)
        {
            uint frameId = BitConverter.ToUInt32(data.Array, data.Offset + 1);
            int playerCount = data.Array[data.Offset + 5];
            InputFrame[] playerInputs = new InputFrame[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerInputs[i] = new InputFrame();
                playerInputs[i].Deserialize(data.Array, data.Offset + 6 + i * 6);
            }

            GameFrame gameFrame = new GameFrame { FrameId = frameId, AllPlayerInputs = playerInputs };
            if (!_serverFrames.ContainsKey(frameId)) _serverFrames.Add(frameId, gameFrame);
        }
        else if (opCode == (byte)ServerOpCode.GameStartNtf)
        {
            StartGame();
        }
    }
}
