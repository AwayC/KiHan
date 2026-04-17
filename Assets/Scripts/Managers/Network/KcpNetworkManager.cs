using UnityEngine;
using kcp2k;
using System;
using KiHan.Logic;

/// <summary>
/// 真实的 KCP 网络层实现
/// </summary>
public class KcpNetworkManager : NetworkManager
{
    private KcpClient _client;

    [Header("KCP Settings")]
    public string ip = "127.0.0.1";
    public ushort port = 9999;

    protected override void Awake()
    {
        base.Awake();
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

    public override void Connect()
    {
        Debug.Log($"[Net] KCP Connecting to {ip}:{port}...");
        _client.Connect(ip, port);
    }

    public override void Send(byte[] data) 
    {
        if (Connected)
        {
            _client.Send(new ArraySegment<byte>(data), KcpChannel.Reliable);
        }
    }

    public override bool Connected => _client != null && _client.connected;

    private void Update()
    {
        _client?.Tick();
    }
}
