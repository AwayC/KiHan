using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using kcp2k;
using System;

public class NetworkManager : UnitySingleton<NetworkManager>
{
    private KcpClient _client;

    public Action OnConnected;
    public Action OnDisconnected;
    public Action<string> OnConnectFailed;
    public Action<ArraySegment<byte>, KcpChannel> OnDataReceived; // Changed from OnFrameDataReceived

    [Header("KCP Settings")]
    public string ip = "127.0.0.1";
    public ushort port = 9999;
    public bool noDelay = true;
    public uint interval = 10; // KCP 内部刷新频率 (ms)

    public bool Connected => _client != null && _client.connected;

    public override void Awake()
    {
        base.Awake();

        KcpConfig config = new KcpConfig
        {
            NoDelay = noDelay,
            Interval = interval,
            FastResend = 2,
            SendWindowSize = 1024,
            ReceiveWindowSize = 1024
        };

        _client = new KcpClient(
                () => {
                    Debug.Log("[KCP] Connected");
                    OnConnected?.Invoke();
                },
                (data, channel) => OnDataReceived?.Invoke(data, channel),
                () => {
                    Debug.Log("[KCP] Disconnected");
                    OnDisconnected?.Invoke();
                },
                (error, reason) => {
                    Debug.LogWarning($"[KCP] Error: {error}, Reason: {reason}");
                    OnConnectFailed?.Invoke(reason);
                },
                config
        );
    }

    public void Connect()
    {
        Debug.Log($"[Network] Connecting to {ip}:{port}...");
        _client.Connect(ip, port);
    }

    public void Disconnect()
    {
        _client.Disconnect();
    }

    /*
     * 发送原始字节数据
     */
    public void Send(byte[] data, KcpChannel channel = KcpChannel.Reliable)
    {
        if (Connected)
        {
            _client.Send(new ArraySegment<byte>(data), channel);
        }
    }

    private void Update()
    {
        _client?.Tick();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}
