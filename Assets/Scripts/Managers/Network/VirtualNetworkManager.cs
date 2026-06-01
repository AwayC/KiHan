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
    private static VirtualNetworkManager _vInstance;
    public new static VirtualNetworkManager Instance
    {
        get
        {
            if (_vInstance == null)
            {
                _vInstance = FindObjectOfType<VirtualNetworkManager>();
                if (_vInstance == null)
                {
                    GameObject obj = new GameObject("VirtualNetworkManager");
                    _vInstance = obj.AddComponent<VirtualNetworkManager>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _vInstance;
        }
    }

    private float _tickRate => GameConfig.LOGIC_TICK_TIME;
    private uint _frameId = 0;
    private Dictionary<byte, InputFrame> _inputQueue = new Dictionary<byte, InputFrame>();
    private Dictionary<byte, InputFrame> _lastInputs = new Dictionary<byte, InputFrame>(); // 缓存最后一次有效输入
    private bool _isRunning = false;

    public override void Connect()
    {
        Debug.Log("[Net] Virtual Mode: Simulating Connection...");
        
        _lastInputs.Clear();
        _inputQueue.Clear();
        
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
            // 存入队列并备份到缓存
            _inputQueue[1] = input;
            _lastInputs[1] = input;
        }
    }

    public override bool Connected => _isRunning;

    private IEnumerator ServerLoop()
    {
        yield return new WaitForSeconds(0.5f);
        // 模拟游戏开始通知
        OnOpCodeReceived?.Invoke(ServerOpCode.GameStartNtf, new ArraySegment<byte>(new byte[0]));

        // 等待前端加载完成
        while (GameApp.Instance == null || !GameApp.Instance.IsGameRunning)
        {
            yield return null;
        }

        float nextTickTime = Time.realtimeSinceStartup;

        while (_isRunning)
        {
            if (Time.realtimeSinceStartup >= nextTickTime)
            {
                _frameId++;
                nextTickTime += _tickRate;

                // 构造 GameFrameUpdate
                List<byte> frameData = new List<byte>();
                frameData.AddRange(BitConverter.GetBytes(_frameId));
                frameData.Add(2); // 模拟 2 个玩家

                // --- 玩家 1 (自己) ---
                frameData.Add(1); 
                if (!_inputQueue.TryGetValue(1, out var input1))
                {
                    // 核心修复：如果没有新包，则沿用上一帧输入包
                    if (!_lastInputs.TryGetValue(1, out input1))
                    {
                        input1 = new InputFrame { FrameId = _frameId, JoyStickAngle = 255, Buttons = ButtonMask.None };
                    }
                }
                byte[] buf1 = new byte[6];
                input1.Serialize(buf1, 0);
                frameData.AddRange(buf1);

                // --- 玩家 2 (静止) ---
                frameData.Add(2);
                InputFrame input2 = new InputFrame { FrameId = _frameId, JoyStickAngle = 255, Buttons = ButtonMask.None };
                byte[] buf2 = new byte[6];
                input2.Serialize(buf2, 0);
                frameData.AddRange(buf2);

                OnOpCodeReceived?.Invoke(ServerOpCode.GameFrameUpdate, new ArraySegment<byte>(frameData.ToArray()));
                _inputQueue.Clear();
            }

            yield return null;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        StopAllCoroutines();
    }
}
