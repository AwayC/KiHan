using UnityEngine;
using kcp2k;
using System;
using System.Collections.Generic;
using KiHan.Logic;
using System.Collections;

/// <summary>
/// 负责模拟服务器行为，实现单机调试
/// </summary>
public class VirtualServer : MonoBehaviour
{
    private float _tickRate = 0.066f; // 15 FPS
    private uint _frameId = 0;
    
    // 缓存收到的玩家输入: GameId -> InputFrame
    private Dictionary<byte, InputFrame> _inputQueue = new Dictionary<byte, InputFrame>();
    private bool _running = false;

    public void StartServer()
    {
        _running = true;
        _frameId = 0;
        StartCoroutine(ServerLoop());
    }

    public void StopServer() => _running = false;

    public void ReceiveInput(byte gId, InputFrame input)
    {
        _inputQueue[gId] = input;
    }

    private IEnumerator ServerLoop()
    {
        // 模拟游戏开始通知
        yield return new WaitForSeconds(0.5f);
        NetworkManager.Instance.Distribute(ServerOpCode.GameStartNtf, new byte[0]);

        while (_running)
        {
            yield return new WaitForSeconds(_tickRate);
            _frameId++;
            
            // 打包所有玩家输入 (这里单机只有自己，ID设为1)
            // 协议格式: [4B frameId] [1B count] [N * 3B data(id, angle, buttons)]
            List<byte> frameData = new List<byte>();
            frameData.AddRange(BitConverter.GetBytes(_frameId));
            
            byte count = (byte)(_inputQueue.Count > 0 ? _inputQueue.Count : 1);
            frameData.Add(count);

            if (_inputQueue.Count > 0)
            {
                foreach (var kv in _inputQueue)
                {
                    frameData.Add(kv.Key);
                    frameData.Add(kv.Value.JoyStickAngle);
                    frameData.Add((byte)kv.Value.Buttons);
                }
            }
            else
            {
                // 如果没收到输入，补一个空输入给 ID 1
                frameData.Add(1);
                frameData.Add(255);
                frameData.Add(0);
            }

            NetworkManager.Instance.Distribute(ServerOpCode.GameFrameUpdate, frameData.ToArray());
        }
    }
}

public class NetworkManager : UnitySingleton<NetworkManager>
{
    private KcpClient _client;
    public Action OnConnected;
    public Action<ServerOpCode, ArraySegment<byte>> OnOpCodeReceived;

    [Header("Settings")]
    public bool isLocalMode = true;
    public string ip = "127.0.0.1";
    public ushort port = 9999;

    private VirtualServer _virtualServer;

    protected override void Awake()
    {
        base.Awake();
        if (isLocalMode)
        {
            _virtualServer = gameObject.AddComponent<VirtualServer>();
        }
        else
        {
            InitKCP();
        }
    }

    private void InitKCP()
    {
        KcpConfig config = new KcpConfig { NoDelay = true, Interval = 10, FastResend = 2 };
        _client = new KcpClient(
            () => OnConnected?.Invoke(),
            (data, channel) => {
                if (data.Count > 0)
                {
                    ServerOpCode op = (ServerOpCode)data.Array[data.Offset];
                    var payload = new ArraySegment<byte>(data.Array, data.Offset + 1, data.Count - 1);
                    OnOpCodeReceived?.Invoke(op, payload);
                }
            },
            () => Debug.Log("[KCP] Disconnected"),
            (error, reason) => Debug.LogWarning($"[KCP] {error}: {reason}"),
            config
        );
    }

    public void Connect()
    {
        if (isLocalMode)
        {
            Debug.Log("[Net] Local Mode: Simulated connection");
            // 模拟分配 ID 1 给自己
            byte[] resp = new byte[6];
            resp[5] = 1; 
            OnOpCodeReceived?.Invoke(ServerOpCode.RoomEnterResp, new ArraySegment<byte>(resp));
            _virtualServer.StartServer();
        }
        else
        {
            _client.Connect(ip, port);
        }
    }

    public void Send(byte[] data)
    {
        if (isLocalMode)
        {
            ClientOpCode op = (ClientOpCode)data[0];
            if (op == ClientOpCode.PlayerFrameInput)
            {
                InputFrame input = new InputFrame();
                input.Deserialize(data, 1);
                _virtualServer.ReceiveInput(1, input);
            }
        }
        else
        {
            _client.Send(new ArraySegment<byte>(data), KcpChannel.Reliable);
        }
    }

    /// <summary>
    /// 供本地服务器模拟广播
    /// </summary>
    public void Distribute(ServerOpCode op, byte[] data)
    {
        OnOpCodeReceived?.Invoke(op, new ArraySegment<byte>(data));
    }

    private void Update() => _client?.Tick();
    public bool Connected => isLocalMode || (_client != null && _client.connected);
}
