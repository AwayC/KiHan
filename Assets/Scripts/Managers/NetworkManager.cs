using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using kcp2k;
using Unity.VisualScripting;
using System;

public class NetworkManager : UnitySingleton<NetworkManager>
{
    private KcpClient _client;

    public Action OnConnected;
    public Action OnDisconnected;
    public Action<ArraySegment<byte>, KcpChannel> OnFrameDataReceived; // 收到帧数据回调

    [Header("KCP Settings")]
    public string ip = "127.0.0.1";
    public ushort port = 9999;
    public bool noDelay = true;
    public uint interval = 10; // KCP 内部刷新频率 (ms)

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
                (data, channel) => OnFrameDataReceived?.Invoke(data, channel),
                () => {
                    Debug.Log("[KCP] Disconnected");
                    OnDisconnected?.Invoke();
                },
                (error, reason) => Debug.LogWarning($"[KCP] Error: {error}, Reason: {reason}"),
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
     * 发送指令
     */
    public void SendInput(byte[] data)
    {
        if (_client.connected)
        {
            _client.Send(new ArraySegment<byte>(data), KcpChannel.Unreliable);
        }
    }

    /*
     * kcp tick驱动
     */
    private void Update()
    {
        _client?.Tick();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}