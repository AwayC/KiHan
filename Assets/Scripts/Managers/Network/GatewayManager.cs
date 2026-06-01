using System;
using System.Collections.Concurrent;
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

    public class GatewayManager : UnitySingleton<GatewayManager>
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;

        public bool IsAuthed { get; private set; } = false;
        public uint ConnId { get; private set; }
        public uint ConnKey { get; private set; }
        public int UdpPort { get; private set; }

        public Action OnAuthSuccess;
        public Action<string> OnAuthFailed;
        public Action<ushort, byte[]> OnMessageReceived;

        private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        public async void Connect(string ip, int port, string token)
        {
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                Debug.LogWarning("[GatewayManager] Already connected.");
                return;
            }

            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            IsAuthed = false;

            string url = $"ws://{ip}:{port}/ws?token={token}";
            try
            {
                Debug.Log($"[GatewayManager] Connecting to {url} ...");
                await _ws.ConnectAsync(new Uri(url), _cts.Token);
                Debug.Log("[GatewayManager] Connected! Waiting for auth response...");
                
                // 开启接收循环
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GatewayManager] Connection failed: {ex.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[8192];
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                try
                {
                    WebSocketReceiveResult result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[GatewayManager] Server closed connection.");
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }

                    if (!IsAuthed && result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleAuthResponse(json);
                    }
                    else if (IsAuthed && result.MessageType == WebSocketMessageType.Binary)
                    {
                        byte[] data = new byte[result.Count];
                        Array.Copy(buffer, data, result.Count);
                        HandleBinaryMessage(data);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GatewayManager] Receive error: {ex.Message}");
                    break;
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
                    Debug.Log("[GatewayManager] Auth Success!");
                    // 抛到主线程执行回调
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
                Debug.LogError($"[GatewayManager] Failed to parse auth JSON: {ex.Message}");
            }
        }

        private void HandleBinaryMessage(byte[] data)
        {
            if (data.Length < 2) return;

            // 读取前 2 字节 CmdID (大端序)
            ushort cmdId = (ushort)((data[0] << 8) | data[1]);

            // 读取剩余部分 Payload
            byte[] payload = new byte[data.Length - 2];
            Array.Copy(data, 2, payload, 0, payload.Length);

            _mainThreadActions.Enqueue(() => OnMessageReceived?.Invoke(cmdId, payload));
        }

        public void Disconnect()
        {
            if (_ws != null)
            {
                if (_ws.State == WebSocketState.Open)
                {
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Logout", CancellationToken.None);
                }
                _ws.Dispose();
                _ws = null;
            }
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            IsAuthed = false;
            Debug.Log("[GatewayManager] Disconnected.");
        }

        public async void SendMsg(ushort cmdId, byte[] payload)
        {
            if (_ws == null || _ws.State != WebSocketState.Open || !IsAuthed)
            {
                Debug.LogWarning("[GatewayManager] Cannot send message: Not connected or not authed.");
                return;
            }

            int payloadLen = payload != null ? payload.Length : 0;
            byte[] buffer = new byte[2 + payloadLen];

            // 写入 CmdID (大端序)
            buffer[0] = (byte)(cmdId >> 8);
            buffer[1] = (byte)(cmdId & 0xFF);

            if (payloadLen > 0)
            {
                Array.Copy(payload, 0, buffer, 2, payloadLen);
            }

            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GatewayManager] Send error: {ex.Message}");
            }
        }

        protected override void OnDestroy()
        {
            Disconnect();
            base.OnDestroy();
        }
    }
}
