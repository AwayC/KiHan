using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using kcp2k;

public class LockstepManager : UnitySingleton<LockstepManager>
{
    [Header("Settings")]
    public const float LOGIC_INTERVAL = 0.066f; 

    private NetworkManager _network; // 注入网络层实现

    public void Init(NetworkManager network)
    {
        _network = network;
        if (_network != null)
        {
            _network.OnOpCodeReceived += HandleNetworkOpCode;
        }
    }

    private uint _currFrameId = 0;   // 当前已执行的帧 ID
    private uint _nextSendFrameId = 1; // 下一帧待发送的输入 ID
    
    private float _timer = 0;
    private bool _gameStarted = false;

    // 存放从服务器收到的确认帧
    private SortedList<uint, RoomFrame> _serverFrames = new SortedList<uint, RoomFrame>();

    public Action<RoomFrame> OnExecuteFrame;  
    public byte MyGameId { get; private set; }

    public void StartGame()
    {
        Debug.Log("[Lockstep] 严格帧同步启动");
        _gameStarted = true;
        _currFrameId = 0;
        _nextSendFrameId = 1;
        _serverFrames.Clear();
    }

    private void OnDestroy()
    {
        if (_network != null)
        {
            _network.OnOpCodeReceived -= HandleNetworkOpCode;
        }
    }

    private void Update()
    {
        if (!_gameStarted) return;

        // 1. 执行服务器确认帧 (有多少跑多少，用于追帧)
        ExecuteServerFrames();

        // 2. 固定频率采集并发送本地输入
        _timer += Time.deltaTime;
        while (_timer >= LOGIC_INTERVAL)
        {
            _timer -= LOGIC_INTERVAL;
            SendInputStep();
        }
    }

    private void SendInputStep()
    {
        InputFrame localInput = CaptureLocalInput(_nextSendFrameId);
        SendInputToServer(localInput);
        _nextSendFrameId++;
    }

    private void ExecuteServerFrames()
    {
        // 严格按顺序执行服务器返回的帧
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
        
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        if (horizontal != 0 || vertical != 0)
        {
            float angle = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;
            input.JoyStickAngle = (byte)(angle / 2); 
        }
        else input.JoyStickAngle = 255;

        ButtonMask buttons = ButtonMask.None;
        if (Input.GetKey(KeyCode.J)) buttons |= ButtonMask.Attack;
        if (Input.GetKey(KeyCode.U)) buttons |= ButtonMask.Skill1;
        input.Buttons = buttons;

        return input;
    }

    private void SendInputToServer(InputFrame input)
    {
        if (_network == null || !_network.Connected) return;

        byte[] data = new byte[7]; 
        data[0] = (byte)ClientOpCode.PlayerFrameInput;
        input.Serialize(data, 1);
        _network.Send(data);
    }

    private void HandleNetworkOpCode(ServerOpCode opCode, ArraySegment<byte> payload)
    {
        if (opCode == ServerOpCode.GameFrameUpdate)
        {
            uint frameId = BitConverter.ToUInt32(payload.Array, payload.Offset);
            int playerCount = payload.Array[payload.Offset + 4];
            
            Dictionary<byte, InputFrame> playerInputs = new Dictionary<byte, InputFrame>();
            for (int i = 0; i < playerCount; i++)
            {
                int offset = payload.Offset + 5 + i * 3; 
                byte gId = payload.Array[offset];
                InputFrame input = new InputFrame { FrameId = frameId };
                input.JoyStickAngle = payload.Array[offset + 1];
                input.Buttons = (ButtonMask)payload.Array[offset + 2];
                playerInputs[gId] = input;
            }

            RoomFrame gameFrame = new RoomFrame { FrameId = frameId, InputFrames = playerInputs };
            if (!_serverFrames.ContainsKey(frameId)) _serverFrames.Add(frameId, gameFrame);
        }
        else if (opCode == ServerOpCode.GameStartNtf)
        {
            StartGame();
        }
        else if (opCode == ServerOpCode.RoomEnterResp)
        {
            MyGameId = payload.Array[payload.Offset + 5];
        }
    }
}
