using System;
using UnityEngine;

namespace KiHan.Network
{
    public class LobbyManager : UnitySingleton<LobbyManager>
    {
        public PlayerInfo MyPlayerInfo { get; private set; }
        public bool NeedsCreateRole { get; private set; }

        public Action<PlayerInfo> OnPlayerDataUpdated;
        public Action<int> OnMatchResponse;
        public Action OnCreateRoleRequired;
        public Action<int> OnCreateRoleResponse;
        public Action<int> OnOnlineCountUpdated;

        private void Start()
        {
            if (GatewayManager.Instance != null)
            {
                GatewayManager.Instance.OnAuthSuccess += OnGatewayAuthSuccess;
                GatewayManager.Instance.OnMessageReceived += OnMessageReceived;

                // 补丁：如果此时已经鉴权成功（错过了事件通知），手动调用一次
                if (GatewayManager.Instance.IsAuthed)
                {
                    OnGatewayAuthSuccess();
                }
            }
        }

        protected override void OnDestroy()
        {
            if (GatewayManager.Instance != null)
            {
                GatewayManager.Instance.OnAuthSuccess -= OnGatewayAuthSuccess;
                GatewayManager.Instance.OnMessageReceived -= OnMessageReceived;
            }
            base.OnDestroy();
        }

        private void OnGatewayAuthSuccess()
        {
            NeedsCreateRole = false;
            // 网关鉴权成功后，按文档发送 1001 LoginReq
            Debug.Log("[LobbyManager] Gateway Authed, sending LoginReq (1001)...");
            var req = new LoginReq();
            GatewayManager.Instance.SendMsg(1001, req.Serialize());
        }

        private void OnMessageReceived(ushort cmdId, byte[] payload)
        {
            Debug.Log($"[LobbyManager] Received CmdID: {cmdId}");
            switch (cmdId)
            {
                case 1001: // LoginRsp
                    HandleLoginRsp(payload);
                    break;
                case 1003: // CreateRoleRsp
                    HandleCreateRoleRsp(payload);
                    break;
                case 1004: // CreateRoleNtf
                    HandleCreateRoleNtf(payload);
                    break;
                case 1008: // GetPlayerDataRsp
                    HandleGetPlayerDataRsp(payload);
                    break;
                case 1009: // GetOnlineCountRsp
                    HandleGetOnlineCountRsp(payload);
                    break;
                case 1005: // MatchGameRsp
                    HandleMatchGameRsp(payload);
                    break;
                default:
                    Debug.LogWarning($"[LobbyManager] Unhandled CmdID: {cmdId}");
                    break;
            }
        }

        private void HandleLoginRsp(byte[] payload)
        {
            var rsp = LoginRsp.Deserialize(payload);
            Debug.Log($"[LobbyManager] LoginRsp: err_code={rsp.err_code}");
            if (rsp.err_code == 0)
            {
                NeedsCreateRole = false;
                MyPlayerInfo = rsp.player;
                // 登录成功后主动拉取一次详细数据
                RequestGetPlayerData();
                // 顺便拉取在线人数
                RequestGetOnlineCount();
            }
            else if (rsp.err_code == -2202) // LOBBY_ERR_PLAYER_NOT_EXISTS
            {
                Debug.Log($"[LobbyManager] Login failed: Player not exists. Triggering CreateRole.");
                NeedsCreateRole = true;
                OnCreateRoleRequired?.Invoke();
            }
            else
            {
                Debug.LogError($"[LobbyManager] Login failed with err_code: {rsp.err_code}");
            }
        }

        private void HandleCreateRoleRsp(byte[] payload)
        {
            var rsp = CreateRoleRsp.Deserialize(payload);
            Debug.Log($"[LobbyManager] CreateRoleRsp: err_code={rsp.err_code}");
            OnCreateRoleResponse?.Invoke(rsp.err_code);
            
            if (rsp.err_code == 0)
            {
                NeedsCreateRole = false;
                // 创建成功后重新走登录或者拉取数据流程
                var req = new LoginReq();
                GatewayManager.Instance.SendMsg(1001, req.Serialize());
            }
        }

        private void HandleCreateRoleNtf(byte[] payload)
        {
            var ntf = CreateRoleNtf.Deserialize(payload);
            Debug.Log($"[LobbyManager] CreateRoleNtf received, player needs to create a role.");
            NeedsCreateRole = true;
            OnCreateRoleRequired?.Invoke();
        }

        private void HandleGetPlayerDataRsp(byte[] payload)
        {
            var rsp = GetPlayerDataRsp.Deserialize(payload);
            Debug.Log($"[LobbyManager] GetPlayerDataRsp: err_code={rsp.err_code}");
            if (rsp.err_code == 0 && rsp.player != null)
            {
                MyPlayerInfo = rsp.player;
                OnPlayerDataUpdated?.Invoke(MyPlayerInfo);
            }
        }

        private void HandleGetOnlineCountRsp(byte[] payload)
        {
            var rsp = GetOnlineCountRsp.Deserialize(payload);
            Debug.Log($"[LobbyManager] GetOnlineCountRsp: count={rsp.online_count}");
            if (rsp.err_code == 0)
            {
                OnOnlineCountUpdated?.Invoke(rsp.online_count);
            }
        }

        private void HandleMatchGameRsp(byte[] payload)
        {
            var rsp = MatchGameRsp.Deserialize(payload);
            Debug.Log($"[LobbyManager] MatchGameRsp: err_code={rsp.err_code}");
            OnMatchResponse?.Invoke(rsp.err_code);
        }

        // --- Public APIs for UI ---

        public void RequestCreateRole(string nickname)
        {
            Debug.Log($"[LobbyManager] Requesting CreateRole (1003) with Nickname={nickname}...");
            var req = new CreateRoleReq { nickname = nickname };
            GatewayManager.Instance.SendMsg(1003, req.Serialize());
        }

        public void RequestGetPlayerData()
        {
            Debug.Log("[LobbyManager] Requesting GetPlayerData (1008)...");
            var req = new GetPlayerDataReq();
            GatewayManager.Instance.SendMsg(1008, req.Serialize());
        }

        public void RequestGetOnlineCount()
        {
            Debug.Log("[LobbyManager] Requesting GetOnlineCount (1009)...");
            var req = new GetOnlineCountReq();
            GatewayManager.Instance.SendMsg(1009, req.Serialize());
        }

        public void RequestMatch(int characterId)
        {
            Debug.Log($"[LobbyManager] Requesting MatchGame (1005) with CharID={characterId}...");
            var req = new MatchGameReq { character_id = characterId };
            GatewayManager.Instance.SendMsg(1005, req.Serialize());
        }
    }
}
