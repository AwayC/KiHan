using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using KiHan.Network;

public class KcpNetworkManager : UnitySingleton<KcpNetworkManager>, INetworkChannel, IKcpCallback
{
    private PoolSegManager.Kcp _kcp;
    private Socket _udpSocket;
    private IPEndPoint _remoteEP;

    private bool _isRunning = false;

    // 9 字节网关头
    private const int HEADER_SIZE = 9;
    private const byte PKT_HANDSHAKE = 0;
    private const byte PKT_KCP = 1;

    public uint ConnId { get; private set; }
    public uint ConnKey { get; private set; }

    public bool IsConnected => _isRunning && _kcp != null;

    public event Action<ushort, byte[]> OnMessageReceived;
    private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

    public void Connect(string ip, ushort port, uint connId, uint connKey)
    {
        if (_isRunning) return;

        Debug.Log($"[KcpNetworkManager] Connect KCP to {ip}:{port}, conv={connId}");
        ConnId = connId;
        ConnKey = connKey;
        _remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);

        // 初始化 KCP
        _kcp = new PoolSegManager.Kcp(connId, this);
        _kcp.NoDelay(1, 10, 2, 1);
        _kcp.WndSize(128, 128);
        _kcp.SetMtu(1400);

        try
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.Connect(_remoteEP);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KcpNetworkManager] UDP Socket error: {ex.Message}");
            return;
        }

        _isRunning = true;

        // 发送 UDP 握手包，让网关建立映射
        SendUdpHandshake();

        // 启动后台收发线程
        Task.Run(KcpUpdateLoop);
        Task.Run(UdpReceiveLoop);

        Debug.Log("[KcpNetworkManager] Connected and background loops started.");
    }

    private void Update()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    private async Task KcpUpdateLoop()
    {
        try
        {
            while (_isRunning)
            {
                if (_kcp != null)
                {
                    _kcp.Update(DateTimeOffset.UtcNow);

                    int len;
                    do
                    {
                        var (buffer, avalidSize) = _kcp.TryRecv();
                        len = avalidSize;
                        if (buffer != null)
                        {
                            var temp = new byte[len];
                            buffer.Memory.Span.Slice(0, len).CopyTo(temp);
                            buffer.Dispose();

                            ProcessKcpPayload(temp);
                        }
                    } while (len > 0);
                }

                await Task.Delay(5); // KCP tick interval ~5ms
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[KcpNetworkManager] UpdateLoop Error: {e}");
        }
    }

    private async Task UdpReceiveLoop()
    {
        byte[] recvBuffer = new byte[8192];
        try
        {
            while (_isRunning)
            {
                if (_udpSocket == null || !_udpSocket.Connected) break;
                if (_udpSocket.Available > 0)
                {
                    int received = _udpSocket.Receive(recvBuffer);
                    if (received > HEADER_SIZE)
                    {
                        ProcessUdpPacket(recvBuffer, received);
                    }
                }
                else
                {
                    await Task.Delay(1);
                }
            }
        }
        catch (SocketException)
        {
            // socket closed
        }
        catch (Exception e)
        {
            Debug.LogError($"[KcpNetworkManager] UdpReceiveLoop Error: {e}");
        }
    }

    private void ProcessUdpPacket(byte[] buffer, int length)
    {
        byte pktType = buffer[8];
        if (pktType == PKT_KCP)
        {
            ReadOnlySpan<byte> kcpRaw = buffer.AsSpan(HEADER_SIZE, length - HEADER_SIZE);
            _kcp.Input(kcpRaw);
        }
    }

    private void ProcessKcpPayload(byte[] data)
    {
        int offset = 0;
        while (offset + 4 <= data.Length)
        {
            ushort frameLen = (ushort)((data[offset] << 8) | data[offset + 1]);
            int totalSize = frameLen + 2; 

            if (offset + totalSize > data.Length) break;

            ushort cmdId = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            int payloadLen = frameLen - 2;
            byte[] payload = new byte[payloadLen > 0 ? payloadLen : 0];
            if (payloadLen > 0)
                Array.Copy(data, offset + 4, payload, 0, payloadLen);

            _mainThreadActions.Enqueue(() => OnMessageReceived?.Invoke(cmdId, payload));

            offset += totalSize;
        }
    }

    public void SendMsg(ushort cmdId, byte[] payload)
    {
        if (!_isRunning || _kcp == null) return;

        int payloadLen = payload != null ? payload.Length : 0;
        ushort frameLen = (ushort)(2 + payloadLen); 
        byte[] buffer = new byte[2 + frameLen];     

        buffer[0] = (byte)(frameLen >> 8);
        buffer[1] = (byte)(frameLen & 0xFF);
        buffer[2] = (byte)(cmdId >> 8);
        buffer[3] = (byte)(cmdId & 0xFF);

        if (payloadLen > 0)
            Array.Copy(payload, 0, buffer, 4, payloadLen);

        _kcp.Send(buffer.AsSpan());
    }

    public void Output(IMemoryOwner<byte> buffer, int avalidLength)
    {
        if (_udpSocket == null || !_isRunning) 
        { 
            buffer.Dispose(); 
            return; 
        }

        try
        {
            byte[] packet = BuildGatewayHeader(PKT_KCP, avalidLength);
            buffer.Memory.Span.Slice(0, avalidLength).CopyTo(packet.AsSpan(HEADER_SIZE));
            _udpSocket.Send(packet);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KcpNetworkManager] UDP Send Error: {ex.Message}");
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private void SendUdpHandshake()
    {
        byte[] handshake = BuildGatewayHeader(PKT_HANDSHAKE);
        try
        {
            _udpSocket.Send(handshake);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KcpNetworkManager] UDP Handshake failed: {ex.Message}");
        }
    }

    private byte[] BuildGatewayHeader(byte packetType, int extraSize = 0)
    {
        byte[] buf = new byte[HEADER_SIZE + extraSize];
        buf[0] = (byte)((ConnId >> 24) & 0xFF);
        buf[1] = (byte)((ConnId >> 16) & 0xFF);
        buf[2] = (byte)((ConnId >> 8) & 0xFF);
        buf[3] = (byte)(ConnId & 0xFF);

        buf[4] = (byte)((ConnKey >> 24) & 0xFF);
        buf[5] = (byte)((ConnKey >> 16) & 0xFF);
        buf[6] = (byte)((ConnKey >> 8) & 0xFF);
        buf[7] = (byte)(ConnKey & 0xFF);

        buf[8] = packetType;
        return buf;
    }

    public void Disconnect()
    {
        _isRunning = false;
        
        if (_udpSocket != null)
        {
            try { _udpSocket.Close(); } catch { }
            _udpSocket = null;
        }

        if (_kcp != null)
        {
            try { _kcp.Dispose(); } catch { }
            _kcp = null;
        }
    }

    protected override void OnDestroy()
    {
        Disconnect();
        base.OnDestroy();
    }
}