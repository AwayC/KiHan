using UnityEngine;
using kcp2k;
using System;
using KiHan.Logic;

public class NetworkManager : UnitySingleton<NetworkManager>
{
    private KcpClient _client;
    public Action OnConnected;
    public Action<ServerOpCode, ArraySegment<byte>> OnOpCodeReceived;

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
                    Console.WriteLine("[Net] Recieved data");
                    OnOpCodeReceived?.Invoke(op, payload);
                }
            },
            () => Debug.Log("[KCP] Disconnected"),
            (error, reason) => Debug.LogWarning($"[KCP] {error}: {reason}"),
            config
        );
    }

    public void Connect() => _client.Connect(ip, port);
    public void Send(byte[] data) => _client.Send(new ArraySegment<byte>(data), KcpChannel.Reliable);
    private void Update() => _client?.Tick();
    public bool Connected => _client != null && _client.connected;
}