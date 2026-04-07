using System;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using kcp2k;

public class LockstepManager : UnitySingleton<LockstepManager>
{
    private uint _currFrameId = 0;
    private bool _gameStarted = false;
    public byte MyGameId { get; private set; }

    public Action<uint, Dictionary<byte, InputFrame>> OnLogicTick;

    private void Start()
    {
        NetworkManager.Instance.OnOpCodeReceived += HandleOpCode;
    }

    private void HandleOpCode(ServerOpCode op, ArraySegment<byte> data)
    {
        switch (op)
        {
            case ServerOpCode.RoomEnterResp:
                MyGameId = data.Array[data.Offset + 5];
                Debug.Log($"[Lockstep] 入场确认，我的 ID: {MyGameId}");
                byte[] sendData = new byte[1];
                sendData[0] = (byte)ClientOpCode.PlayerReadyReq;
                NetworkManager.Instance.Send(sendData);
                break;

            case ServerOpCode.GameStartNtf:
                StartGame();
                break;

            case ServerOpCode.GameFrameUpdate:
                if (_gameStarted) ProcessNetworkFrame(data);
                break;

            case ServerOpCode.PlayerReadyResp:
                var code = data.Array[data.Offset];
                Debug.Log($"[Lockstep] Ready response: {code}");
                break;
        }
    }

    private void StartGame()
    {
        if (_gameStarted) return;
        _gameStarted = true;
        _currFrameId = 0;

        GameApp.Instance.GameStart();
    }

    private void ProcessNetworkFrame(ArraySegment<byte> data)
    {
        uint frameId = BitConverter.ToUInt32(data.Array, data.Offset);
        byte playerCount = data.Array[data.Offset + 4];

        Debug.Log($"frameId: {frameId}");

        var allInputs = new Dictionary<byte, InputFrame>();
        for (int i = 0; i < playerCount; i++)
        {
            int offset = data.Offset + 5 + i * 3; // 1:id, 1:angle, 1:buttons
            byte gId = data.Array[offset];
            InputFrame input = new InputFrame { FrameId = frameId };
            input.JoyStickAngle = data.Array[offset + 1];
            input.Buttons = (ButtonMask)data.Array[offset + 2];
            allInputs[gId] = input;
        }

        _currFrameId = frameId;
        OnLogicTick?.Invoke(frameId, allInputs);

        CaptureAndSendInput();
    }

    private void CaptureAndSendInput()
    {
        // 当前不支持 joystick
        // todo: 支持 joystick
        InputFrame input = new InputFrame { FrameId = _currFrameId + 1 };
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (h != 0 || v != 0)
        {
            float angle = Mathf.Atan2(v, h) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;
            input.JoyStickAngle = (byte)(angle / 2);
        }
        else input.JoyStickAngle = 255;

        input.Buttons = ButtonMask.None;
        if (Input.GetKey(KeyCode.J)) input.Buttons |= ButtonMask.Attack;
        if (Input.GetKey(KeyCode.U)) input.Buttons |= ButtonMask.Skill1;

        byte[] sendData = new byte[7];
        sendData[0] = (byte)ClientOpCode.PlayerFrameInput;
        input.Serialize(sendData, 1);
        NetworkManager.Instance.Send(sendData);
    }
}