using KiHan.Network;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 大厅：玩家昵称 / UID、胜率 / 胜场 / 在线人数 (LobbyManager 事件刷新)，开始匹配、单机开始，返回 / 关闭确认后登出
    /// - 需要创建角色时打开 CreateRolePopup，开始匹配打开 MatchingPopup
    /// - 文本只在数值变化时刷新
    /// </summary>
    [UIPanel("UI/Panels/Lobby/LobbyPanel", UILayer.Normal)]
    public partial class LobbyPanel : UIPanel
    {
        private const string LogoutConfirmText = "确定返回登录界面吗？";
        private const string WinCountSuffix = "场";

        /// <summary>
        /// PlayerInfo.data_json 的内容
        /// </summary>
        [System.Serializable]
        private class PlayerDataJson
        {
            public int total_battle_count;
            public int win_count;
        }

        private static readonly string[] RateTexts = new string[101];

        private int _shownOnlineCount = -1;
        private int _shownWinCount = -1;
        private int _shownWinRate = -1;
        private uint _shownUid;
        private bool _uidShown;
        private string _shownNickname;

        protected override void OnCreate()
        {
            _closeBtn.onClick.AddListener(OnBackClicked);
            _backBtn.onClick.AddListener(OnBackClicked);
            _startBtn.onClick.AddListener(OnStartMatchClicked);
            _offlineStartBtn.onClick.AddListener(OnOfflineStartClicked);
        }

        protected override void OnOpen()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby == null) return;

            // 已打开时再次 Open 会重新进入 OnOpen，先退订避免重复订阅
            Unsubscribe(lobby);
            lobby.OnPlayerDataUpdated += OnPlayerDataUpdated;
            lobby.OnCreateRoleRequired += OnCreateRoleRequired;
            lobby.OnOnlineCountUpdated += OnOnlineCountUpdated;

            // 已有缓存数据时直接刷新一次
            if (lobby.MyPlayerInfo != null) OnPlayerDataUpdated(lobby.MyPlayerInfo);

            // 主动拉取在线人数
            lobby.RequestGetOnlineCount();

            // 打开大厅前就已经收到了创建角色通知
            if (lobby.NeedsCreateRole) OnCreateRoleRequired();
        }

        protected override void OnClose()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby != null) Unsubscribe(lobby);
        }

        private void Unsubscribe(LobbyManager lobby)
        {
            lobby.OnPlayerDataUpdated -= OnPlayerDataUpdated;
            lobby.OnCreateRoleRequired -= OnCreateRoleRequired;
            lobby.OnOnlineCountUpdated -= OnOnlineCountUpdated;
        }

        #region 数据回调

        private void OnPlayerDataUpdated(PlayerInfo info)
        {
            if (info == null) return;
            SetProfile(info.nickname, info.uid);

            int total = 0;
            int win = 0;
            if (!string.IsNullOrEmpty(info.data_json))
            {
                try
                {
                    PlayerDataJson data = JsonUtility.FromJson<PlayerDataJson>(info.data_json);
                    if (data != null)
                    {
                        total = data.total_battle_count;
                        win = data.win_count;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyPanel] 解析玩家数据失败: {e.Message}");
                    total = 0;
                    win = 0;
                }
            }

            SetWinCount(win);
            SetWinRate(total > 0 ? Mathf.RoundToInt((float)win / total * 100f) : 0);
        }

        private void OnCreateRoleRequired()
        {
            UIManager.Instance.Open<CreateRolePopup>();
        }

        private void OnOnlineCountUpdated(int count)
        {
            if (count == _shownOnlineCount) return;
            _shownOnlineCount = count;
            _onlineCnt.text = count.ToString();
        }

        #endregion

        #region 显示

        private void SetProfile(string nickname, uint uid)
        {
            if (nickname != _shownNickname)
            {
                _shownNickname = nickname;
                _nickname.text = nickname ?? string.Empty;
            }

            if (!_uidShown || uid != _shownUid)
            {
                _uidShown = true;
                _shownUid = uid;
                _uid.text = uid.ToString();
            }
        }

        private void SetWinCount(int count)
        {
            if (count == _shownWinCount) return;
            _shownWinCount = count;
            _winCnt.text = count.ToString() + WinCountSuffix;
        }

        private void SetWinRate(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (percent == _shownWinRate) return;
            _shownWinRate = percent;
            _winRate.text = RateTexts[percent] ??= percent.ToString() + "%";
        }

        #endregion

        #region 按钮

        private void OnBackClicked()
        {
            UIManager.Instance.Confirm(LogoutConfirmText, () => GameFlow.Instance.Logout());
        }

        private void OnStartMatchClicked()
        {
            UIManager.Instance.Open<MatchingPopup>();
        }

        private void OnOfflineStartClicked()
        {
            GameFlow.Instance.StartOfflineBattle();
        }

        #endregion
    }
}
