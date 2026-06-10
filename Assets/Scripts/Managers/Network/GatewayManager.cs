using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KiHan.Network
{
    [Serializable]
    public class AuthResponse
    {
        public int code;
        public string msg;
        public uint conn_id;
        public uint key;
        public int udp_port;
    }

    /// <summary>
    /// 统一网关：Lobby(< 2000) 走 WebSocket，Game(>= 2000) 走 KCP/UDP。
    /// </summary>
    public class GatewayManager : UnitySingleton<GatewayManager>, INetworkChannel, IKcpCallback
    {
        // --- WebSocket ---
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;

        // --- KCP ---
        private SimpleSegManager.Kcp _kcp;
        private Socket _udpSocket;
        private byte[] _udpRecvBuffer = new byte[8192];
        private bool _kcpReady = false;
        private string _serverIp;
        public string ServerIp => _serverIp;

        // 9 字节网关头
        private const int HEADER_SIZE = 9;
        private const byte PKT_HANDSHAKE = 0;
        private const byte PKT_KCP = 1;

        // --- 公共状态 ---
        public bool IsAuthed { get; private set; } = false;
        public bool IsConnected => IsAuthed;
        public uint ConnId { get; private set; }
        public uint ConnKey { get; private set; }
        public int UdpPort { get; private set; }

        public Action OnAuthSuccess;
        public Action<string> OnAuthFailed;
        public event Action<ushort, byte[]> OnMessageReceived;

        private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        private void Update()
        {
            // 主线程回调队列
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }

            // KCP 主循环
            //if (_kcp != null && _kcpReady)
            //{
            //    ReceiveUdp();
            //    _kcp.Update(DateTimeOffset.UtcNow);
            //    DrainKcpRecv();
            //}
        }

        #region WebSocket

        public async void Connect(string ip, int port, string token)
        {
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                Debug.LogWarning("[GatewayManager] Already connected.");
                return;
            }

            _serverIp = ip;
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            IsAuthed = false;

            string url = $"ws://{ip}:{port}/ws?token={token}";
            try
            {
                Debug.Log($"[GatewayManager] Connecting to {url} ...");
                await _ws.ConnectAsync(new Uri(url), _cts.Token);
                Debug.Log("[GatewayManager] WS Connected! Waiting for auth...");
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GatewayManager] WS Connection failed: {ex.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[8192];
            using (var ms = new System.IO.MemoryStream())
            {
                while (_ws != null && _ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.Log("[GatewayManager] Server closed WS.");
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            break;
                        }

                        // 把收到的数据写进内存流解决分片/粘包问题
                        ms.Write(buffer, 0, result.Count);

                        if (result.EndOfMessage)
                        {
                            byte[] messageData = ms.ToArray();
                            ms.SetLength(0); // 重置缓冲区

                            if (!IsAuthed && result.MessageType == WebSocketMessageType.Text)
                            {
                                string json = Encoding.UTF8.GetString(messageData);
                                HandleAuthResponse(json);
                            }
                            else if (IsAuthed && result.MessageType == WebSocketMessageType.Binary)
                            {
                                // WebSocket 回调：安全可靠的纯二进制解析
                                Debug.Log("[Gateway] handle ws message");
                                HandleWsBinaryMessage(messageData);
                            }
                            else if (IsAuthed && result.MessageType == WebSocketMessageType.Text)
                            {
                                Debug.LogWarning($"[GatewayManager] Unexpected WS Text data while authed: {Encoding.UTF8.GetString(messageData)}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_cts.IsCancellationRequested)
                            Debug.LogError($"[GatewayManager] WS Receive error: {ex.Message}");
                        break;
                    }
                }
            }
            IsAuthed = false;
        }

        private void HandleAuthResponse(string json)
        {
            Debug.Log($"[GatewayManager] Auth Response: {json}");
            try
            {
                AuthResponse res = JsonUtility.FromJson<AuthResponse>(json);
                if (res.code == 0)
                {
                    IsAuthed = true;
                    ConnId = res.conn_id;
                    ConnKey = res.key;
                    UdpPort = res.udp_port;
                    Debug.Log($"[GatewayManager] Auth OK! ConnId={ConnId}, Key={ConnKey}, UdpPort={UdpPort}");

                    // 用闭包捕获所有值，避免主线程读到旧值的线程安全问题
                    //if (res.udp_port > 0)
                    //{
                    //    uint cId = res.conn_id;
                    //    uint cKey = res.key;
                    //    string cIp = _serverIp;
                    //    ushort cPort = (ushort)res.udp_port;
                    //    _mainThreadActions.Enqueue(() => DoConnectKcp(cIp, cPort, cId, cKey));
                    //}

                    _mainThreadActions.Enqueue(() => OnAuthSuccess?.Invoke());
                }
                else
                {
                    Debug.LogError($"[GatewayManager] Auth Failed: {res.msg}");
                    _mainThreadActions.Enqueue(() => OnAuthFailed?.Invoke(res.msg));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GatewayManager] Parse auth JSON failed: {ex.Message}");
            }
        }

        /// <summary>
        /// WS 收到的二进制消息（Lobby 响应，cmdId < 2000）
        /// </summary>
        private void HandleWsBinaryMessage(byte[] data)
        {
            if (data.Length < 2) return;
            
            ushort cmdId = (ushort)((data[0] << 8) | data[1]);
            byte[] payload = new byte[data.Length - 2];
            if (payload.Length > 0)
            {
                Array.Copy(data, 2, payload, 0, payload.Length);
            }
            
            // 扔到主线程去触发回调，这样就能更新 UI 面板了
            _mainThreadActions.Enqueue(() => OnMessageReceived?.Invoke(cmdId, payload));
        }

        #endregion

        #region KCP

        /// <summary>
        /// 在主线程创建 KCP 实例和 UDP Socket
        /// </summary>
        private void DoConnectKcp(string ip, ushort port, uint connId, uint connKey)
        {
            Debug.Log($"[GatewayManager] Creating KCP on main thread, target={ip}:{port}");

            ConnId = connId;
            ConnKey = connKey;

            _kcp = new SimpleSegManager.Kcp(connId, this);
            _kcp.NoDelay(1, 10, 2, 1);
            _kcp.WndSize(128, 128);

            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);
                _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _udpSocket.Blocking = false;
                _udpSocket.Connect(remoteEP);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GatewayManager] UDP Socket creation failed: {ex.Message}");
                return;
            }

            // 发送 UDP 握手包 (PacketType=0)
            SendUdpHandshake();

            _kcpReady = true;
            Debug.Log("[GatewayManager] KCP Ready!");
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
                Debug.LogError($"[GatewayManager] UDP Handshake failed: {ex.Message}");
            }
        }

        private byte[] BuildGatewayHeader(byte packetType, int extraSize = 0)
        {
            byte[] buf = new byte[HEADER_SIZE + extraSize];
            // conn_id (大端)
            buf[0] = (byte)((ConnId >> 24) & 0xFF);
            buf[1] = (byte)((ConnId >> 16) & 0xFF);
            buf[2] = (byte)((ConnId >> 8) & 0xFF);
            buf[3] = (byte)(ConnId & 0xFF);
            // key (大端)
            buf[4] = (byte)((ConnKey >> 24) & 0xFF);
            buf[5] = (byte)((ConnKey >> 16) & 0xFF);
            buf[6] = (byte)((ConnKey >> 8) & 0xFF);
            buf[7] = (byte)(ConnKey & 0xFF);
            // PacketType
            buf[8] = packetType;
            return buf;
        }

        /// <summary>
        /// IKcpCallback.Output: KCP 要发数据时的回调
        /// </summary>
        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            if (_udpSocket == null) { buffer.Dispose(); return; }

            try
            {
                byte[] packet = BuildGatewayHeader(PKT_KCP, avalidLength);
                buffer.Memory.Span.Slice(0, avalidLength).CopyTo(packet.AsSpan(HEADER_SIZE));
                _udpSocket.Send(packet);
            }
            catch (Exception)
            {
                // Ignore UDP send errors in fast loop
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private void ReceiveUdp()
        {
            if (_udpSocket == null) return;

            for (int i = 0; i < 50; i++)
            {
                try
                {
                    if (_udpSocket.Available <= 0) break;
                    int received = _udpSocket.Receive(_udpRecvBuffer);
                    if (received <= HEADER_SIZE) continue;

                    byte pktType = _udpRecvBuffer[8];
                    if (pktType == PKT_KCP)
                    {
                        ReadOnlySpan<byte> kcpRaw = _udpRecvBuffer.AsSpan(HEADER_SIZE, received - HEADER_SIZE);
                        _kcp.Input(kcpRaw);
                    }
                }
                catch (SocketException)
                {
                    break;
                }
            }
        }

        private void DrainKcpRecv()
        {
            while (true)
            {
                var (buf, len) = _kcp.TryRecv();
                if (buf == null || len <= 0) break;

                byte[] data = new byte[len];
                buf.Memory.Span.Slice(0, len).CopyTo(data);
                buf.Dispose();

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

                    // 抛给主线程的回调处理战斗帧
                    _mainThreadActions.Enqueue(() => OnMessageReceived?.Invoke(cmdId, payload));

                    offset += totalSize;
                }
            }
        }

        #endregion

        #region SendMsg

        public async void SendMsg(ushort cmdId, byte[] payload)
        {
            if (cmdId >= 2000)
            {
                SendViaKcp(cmdId, payload);
            }
            else
            {
                await SendViaWs(cmdId, payload);
            }
        }

        private void SendViaKcp(ushort cmdId, byte[] payload)
        {
            if (_kcp == null || !_kcpReady) return;

            int payloadLen = payload != null ? payload.Length : 0;
            ushort frameLen = (ushort)(2 + payloadLen); // CmdID(2) + Payload
            byte[] buffer = new byte[2 + frameLen];     // Len(2) + CmdID(2) + Payload

            buffer[0] = (byte)(frameLen >> 8);
            buffer[1] = (byte)(frameLen & 0xFF);
            buffer[2] = (byte)(cmdId >> 8);
            buffer[3] = (byte)(cmdId & 0xFF);

            if (payloadLen > 0)
                Array.Copy(payload, 0, buffer, 4, payloadLen);

            _kcp.Send(buffer.AsSpan());
        }

        private async Task SendViaWs(ushort cmdId, byte[] payload)
        {
            if (_ws == null || _ws.State != WebSocketState.Open || !IsAuthed) return;

            int payloadLen = payload != null ? payload.Length : 0;
            byte[] buffer = new byte[2 + payloadLen];
            buffer[0] = (byte)(cmdId >> 8);
            buffer[1] = (byte)(cmdId & 0xFF);
            if (payloadLen > 0)
                Array.Copy(payload, 0, buffer, 2, payloadLen);

            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GatewayManager] WS Send error: {ex.Message}");
            }
        }

        #endregion

        #region Disconnect

        public void Disconnect()
        {
            _kcpReady = false;
            _kcp = null;

            if (_udpSocket != null)
            {
                try { _udpSocket.Close(); } catch { }
                _udpSocket = null;
            }

            _cts?.Cancel();
            if (_ws != null)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Logout", CancellationToken.None);
                }
                catch { }
                _ws.Dispose();
                _ws = null;
            }
            _cts?.Dispose();
            _cts = null;

            IsAuthed = false;
            Debug.Log("[GatewayManager] Disconnected.");
        }

        protected override void OnDestroy()
        {
            Disconnect();
            base.OnDestroy();
        }

        #endregion
    }
}