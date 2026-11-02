using KiHan.Network;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 匹配等待弹窗：打开时发起匹配并计时，取消 / 关闭时取消匹配；
    /// 匹配成功立即进入联机战斗 (不等界面动画)，1 秒后关闭自己；匹配失败自动取消
    /// </summary>
    [UIPanel("UI/Panels/Lobby/MatchingPopup", UILayer.Popup)]
    public partial class MatchingPopup : UIPanel
    {
        private const string SearchingText = "正在寻找旗鼓相当的对手...";
        private const string MatchedText = "匹配成功，正在进入战斗...";
        private const string SecondSuffix = "秒";

        /// <summary>
        /// 暂定角色 ID
        /// </summary>
        private const int CharacterId = 1;

        private const float CloseDelayAfterMatched = 1f;
        private const int MaxCachedSeconds = 3600;

        private static readonly int LoadedState = Animator.StringToHash("loaded");
        private static readonly string[] SecondTexts = new string[MaxCachedSeconds];

        private enum State
        {
            Idle,
            Matching,
            Matched,
        }

        private State _state;
        private float _elapsed;
        private int _shownSeconds;
        private float _closeCountdown;

        protected override void OnCreate()
        {
            _cancelBtn.onClick.AddListener(OnCancelClicked);
        }

        protected override void OnOpen()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby == null) return;

            Unsubscribe(lobby);
            lobby.OnMatchNtfReceived += OnMatchNtf;
            lobby.OnMatchCancelResponse += OnMatchCancelResponse;

            _state = State.Matching;
            _elapsed = 0f;
            _shownSeconds = 0;
            _info.text = SearchingText;
            _waitTime.text = SecondsText(0);

            lobby.RequestMatch(CharacterId);
        }

        protected override void OnClose()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby == null) return;
            Unsubscribe(lobby);

            // 匹配中被关闭 (返回键 / 流程切换)：通知服务器取消，避免关闭后仍被匹配进房间
            if (_state == State.Matching) lobby.CancelMatch();
            _state = State.Idle;
        }

        private void Unsubscribe(LobbyManager lobby)
        {
            lobby.OnMatchNtfReceived -= OnMatchNtf;
            lobby.OnMatchCancelResponse -= OnMatchCancelResponse;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Matching:
                    _elapsed += Time.unscaledDeltaTime;
                    int seconds = (int)_elapsed;
                    if (seconds != _shownSeconds)
                    {
                        _shownSeconds = seconds;
                        _waitTime.text = SecondsText(seconds);
                    }
                    break;

                case State.Matched:
                    _closeCountdown -= Time.unscaledDeltaTime;
                    if (_closeCountdown <= 0f)
                    {
                        _state = State.Idle;
                        Close();
                    }
                    break;
            }
        }

        private static string SecondsText(int seconds)
        {
            if (seconds >= MaxCachedSeconds) seconds = MaxCachedSeconds - 1;
            return SecondTexts[seconds] ??= seconds.ToString() + SecondSuffix;
        }

        private void OnCancelClicked()
        {
            LobbyManager.Instance?.CancelMatch();
        }

        private void OnMatchCancelResponse(int errCode, bool success)
        {
            if (!success || _state != State.Matching) return;
            _state = State.Idle;
            Close();
        }

        private void OnMatchNtf(MatchGameNtf ntf)
        {
            if (ntf.err_code != 0)
            {
                Debug.LogError($"[MatchingPopup] 匹配失败: {ntf.err_code}");
                LobbyManager.Instance?.CancelMatch();
                return;
            }

            _state = State.Matched;
            _closeCountdown = CloseDelayAfterMatched;
            _info.text = MatchedText;
            _loading.Play(LoadedState);

            // 立即启动战斗初始化 (注册帧同步回调)，不能等界面动画
            if (uint.TryParse(ntf.room_id, out uint roomId))
            {
                GameFlow.Instance.StartOnlineBattle(roomId);
            }
            else
            {
                Debug.LogError($"[MatchingPopup] 房间号无效: {ntf.room_id}");
            }
        }
    }
}
