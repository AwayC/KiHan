using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using kcp2k;

public class LockstepManager : UnitySingleton<LockstepManager>
{
    [Header("Settings")]
    public const float LOGIC_INTERVAL = 0.066f; 
    public int maxCatchUpPerFrame = 20; 

    private uint _confirmedFrameId = 0;   
    private uint _predictedFrameId = 0;   
    
    private float _timer = 0;
    private bool _gameStarted = false;

    private SortedList<uint, GameFrame> _serverFrames = new SortedList<uint, GameFrame>();
    private Dictionary<uint, InputFrame> _localInputHistory = new Dictionary<uint, InputFrame>();

    public Action<GameFrame> OnExecuteFrame;  
    public Action<uint> OnSaveSnapshot;       
    public Action<uint> OnLoadSnapshot;       

    public void StartGame()
    {
        Debug.Log("[Lockstep] Game Started");
        _gameStarted = true;
        _confirmedFrameId = 0;
        _predictedFrameId = 0;
        _serverFrames.Clear();
        _localInputHistory.Clear();
        OnSaveSnapshot?.Invoke(0); 
    }

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnOpCodeReceived += HandleNetworkOpCode;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnOpCodeReceived -= HandleNetworkOpCode;
        }
    }

    private void Update()
    {
        if (!_gameStarted) return;

        CheckServerFramesAndRollback();

        _timer += Time.deltaTime;
        if (_timer >= LOGIC_INTERVAL)
        {
            _timer -= LOGIC_INTERVAL;
            PredictNextFrame();
        }
    }

    private void PredictNextFrame()
    {
        _predictedFrameId++;
        
        InputFrame localInput = CaptureLocalInput(_predictedFrameId);
        _localInputHistory[_predictedFrameId] = localInput;

        GameFrame predictFrame = new GameFrame
        {
            FrameId = _predictedFrameId,
            AllPlayerInputs = new InputFrame[] { localInput } 
        };

        OnExecuteFrame?.Invoke(predictFrame);
        OnSaveSnapshot?.Invoke(_predictedFrameId);

        SendInputToServer(localInput);
    }

    private void CheckServerFramesAndRollback()
    {
        while (_serverFrames.ContainsKey(_confirmedFrameId + 1))
        {
            uint nextFrameId = _confirmedFrameId + 1;
            GameFrame serverFrame = _serverFrames[nextFrameId];

            OnLoadSnapshot?.Invoke(_confirmedFrameId);
            OnExecuteFrame?.Invoke(serverFrame);
            
            _confirmedFrameId = nextFrameId;
            OnSaveSnapshot?.Invoke(_confirmedFrameId);
            
            _localInputHistory.Remove(_confirmedFrameId);
            _serverFrames.Remove(_confirmedFrameId);

            ReSimulate();
        }
    }

    private void ReSimulate()
    {
        uint tempFrameId = _confirmedFrameId;
        while (tempFrameId < _predictedFrameId)
        {
            tempFrameId++;
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
        NetworkManager.Instance.Send(data);
    }

    private void HandleNetworkOpCode(ServerOpCode opCode, ArraySegment<byte> payload)
    {
        if (opCode == ServerOpCode.GameFrameUpdate)
        {
            uint frameId = BitConverter.ToUInt32(payload.Array, payload.Offset);
            int playerCount = payload.Array[payload.Offset + 4];
            InputFrame[] playerInputs = new InputFrame[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerInputs[i] = new InputFrame();
                playerInputs[i].Deserialize(payload.Array, payload.Offset + 5 + i * 6);
            }

            GameFrame gameFrame = new GameFrame { FrameId = frameId, AllPlayerInputs = playerInputs };
            if (!_serverFrames.ContainsKey(frameId)) _serverFrames.Add(frameId, gameFrame);
        }
        else if (opCode == ServerOpCode.GameStartNtf)
        {
            StartGame();
        }
    }
}
