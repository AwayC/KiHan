using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using kcp2k;
using Managers;

public class LockstepManager : UnitySingleton<LockstepManager>
{
    public static float LOGIC_INTERVAL => GameConfig.LOGIC_TICK_TIME; 

    private NetworkManager _network; 


    public void Init(NetworkManager network)
    {
        _network = network;
        if (_network != null)
        {
            _network.OnOpCodeReceived += HandleNetworkOpCode;
        }
    }

    private uint _currFrameId = 0;   
    private uint _nextSendFrameId = 1; 
    
    private float _timer = 0;
    private bool _gameStarted = false;

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

        ExecuteServerFrames();

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
            // 协议格式：[1:gId][6:input]
            for (int i = 0; i < playerCount; i++)
            {
                int offset = payload.Offset + 5 + i * 7; 
                byte gId = payload.Array[offset];
                InputFrame input = new InputFrame();
                input.Deserialize(payload.Array, offset + 1);
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
