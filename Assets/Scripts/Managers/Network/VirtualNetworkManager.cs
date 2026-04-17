using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using KiHan.Logic;

/// <summary>
/// 虚拟网络层，用于单机模拟
/// </summary>
public class VirtualNetworkManager : NetworkManager
{
    private float _tickRate = 0.066f;
    private uint _frameId = 0;
    private Dictionary<byte, InputFrame> _inputQueue = new Dictionary<byte, InputFrame>();
    private bool _isRunning = false;

    public override void Connect()
    {
        Debug.Log("[Net] Virtual Mode: Simulating Connection...");
        
        // 模拟连接成功，分配 ID 1 给自己
        byte[] resp = new byte[6];
        resp[5] = 1; 
        OnOpCodeReceived?.Invoke(ServerOpCode.RoomEnterResp, new ArraySegment<byte>(resp));
        
        // 启动模拟服务器行为
        _isRunning = true;
        _frameId = 0;
        StartCoroutine(ServerLoop());
    }

    public override void Send(byte[] data)
    {
        if (!_isRunning) return;

        ClientOpCode op = (ClientOpCode)data[0];
        if (op == ClientOpCode.PlayerFrameInput)
        {
            InputFrame input = new InputFrame();
            input.Deserialize(data, 1);
            // 本地模拟，固定发给 ID 1
            _inputQueue[1] = input;
        }
    }

    public override bool Connected => _isRunning;

    private IEnumerator ServerLoop()
    {
        yield return new WaitForSeconds(0.2f);
        // 模拟游戏开始
        OnOpCodeReceived?.Invoke(ServerOpCode.GameStartNtf, new ArraySegment<byte>(new byte[0]));

        while (_isRunning)
        {
            yield return new WaitForSeconds(_tickRate);
            _frameId++;
            
            // 构造 GameFrameUpdate
            List<byte> frameData = new List<byte>();
            frameData.AddRange(BitConverter.GetBytes(_frameId));
            frameData.Add(1); // 1 Player

            InputFrame input;
            if (!_inputQueue.TryGetValue(1, out input))
            {
                input = new InputFrame { FrameId = _frameId, JoyStickAngle = 255, Buttons = ButtonMask.None };
            }
            
            byte[] inputBytes = new byte[6];
            input.Serialize(inputBytes, 0);
            frameData.AddRange(inputBytes);

            OnOpCodeReceived?.Invoke(ServerOpCode.GameFrameUpdate, new ArraySegment<byte>(frameData.ToArray()));
            _inputQueue.Clear();
        }
    }

    public void Stop()
    {
        _isRunning = false;
        StopAllCoroutines();
    }
}
