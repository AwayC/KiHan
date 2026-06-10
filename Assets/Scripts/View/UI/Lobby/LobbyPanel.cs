using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KiHan.View.UI.Login;
using Managers;

namespace KiHan.View.UI.Lobby
{
    public class LobbyPanel : BasePanel
    {
        [Header("Components")]
        private RectTransform _title;
        private RectTransform _closeBtn;
        private RectTransform _backBtn;
        private RectTransform _userProfile;
        
        // Start Group
        private RectTransform _startPanel;
        private Button _realStartBtn;
        private Button _offlineStartBtn;
        
        // Match Panel
        private GameObject _matchPanelGo;
        private TMP_Text _matchInfoText;
        private TMP_Text _matchWaitTimeText;
        private Animator _matchLoadingAnimator;
        private Coroutine _matchTimerCoroutine;
        private float _matchWaitTime = 0f;

        [Header("Profile Info")]
        private Button _userProfileBtn;
        private TMP_Text _nicknameText;
        private TMP_Text _uidText;

        [Header("Create Player Info")]
        private RectTransform _createPlayerPanel;
        private TMP_InputField _createRoleInput;
        private Button _createRoleBtn;

        [Header("Data Display")]
        private TMP_Text _onlineCntText;
        private TMP_Text _winRateText;
        private TMP_Text _winCntText;

        private GameObject _backPanel;
        private Dictionary<RectTransform, Vector2> _originalPositions = new Dictionary<RectTransform, Vector2>();

        // 把原来的 Init() 改回 Awake()，UIManager 不会调用 Init，只有 Unity 会调用 Awake
        private void Awake()
        {
            // Bind RectTransforms for animation
            // 优先查找根节点的直接子物体，防止和深层节点的同名物体冲突
            _title = transform.Find("Title")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "Title")?.GetComponent<RectTransform>();
            _closeBtn = transform.Find("CloseBtn")?.GetComponent<RectTransform>() ?? transform.Find("xBtn")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "CloseBtn")?.GetComponent<RectTransform>();
            _backBtn = transform.Find("BackBtn")?.GetComponent<RectTransform>() ?? transform.Find("backBtn")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "BackBtn")?.GetComponent<RectTransform>();
            _userProfile = transform.Find("UserProfile")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "UserProfile")?.GetComponent<RectTransform>();
            _startPanel = transform.Find("StartPanel")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "StartPanel")?.GetComponent<RectTransform>();
            _createPlayerPanel = transform.Find("CreatePlayerPanel")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "CreatePlayerPanel")?.GetComponent<RectTransform>();

            // Back Panel (Exit Confirmation)
            _backPanel = transform.Find("BackPanel")?.gameObject ?? FindChild(gameObject, "BackPanel");
            if (_backPanel != null)
            {
                Transform container = _backPanel.transform.Find("container");
                if (container != null)
                {
                    Button xBtn = container.Find("XBtn")?.GetComponent<Button>() ?? container.GetComponentInChildren<Button>(true);
                    
                    Transform btnsRoot = container.Find("Btns");
                    Button cancelBtn = btnsRoot?.Find("CancelBtn")?.GetComponent<Button>() ?? FindChild(container.gameObject, "CancelBtn")?.GetComponent<Button>();
                    Button confirmBtn = btnsRoot?.Find("ConfirmBtn")?.GetComponent<Button>() ?? FindChild(container.gameObject, "ConfirmBtn")?.GetComponent<Button>();

                    xBtn?.onClick.AddListener(() => _backPanel.SetActive(false));
                    cancelBtn?.onClick.AddListener(() => _backPanel.SetActive(false));
                    confirmBtn?.onClick.AddListener(() => {
                        _backPanel.SetActive(false);
                        OnRealLogout();
                    });
                }
                _backPanel.SetActive(false);
            }

            // Bind StartBtn, OfflineStartBtn and Info
            if (_startPanel != null)
            {
                _realStartBtn = _startPanel.Find("StartBtn")?.GetComponent<Button>() ?? FindChild(_startPanel.gameObject, "StartBtn")?.GetComponent<Button>();
                _offlineStartBtn = _startPanel.Find("OfflineStartBtn")?.GetComponent<Button>() ?? FindChild(_startPanel.gameObject, "OfflineStartBtn")?.GetComponent<Button>();
                GameObject infoGo = _startPanel.Find("Info")?.gameObject ?? FindChild(_startPanel.gameObject, "Info");
                if (infoGo != null)
                {
                    _onlineCntText = infoGo.transform.Find("onlineCnt/num")?.GetComponent<TMP_Text>() ?? FindChild(infoGo, "onlineCnt")?.transform.Find("num")?.GetComponent<TMP_Text>();
                    _winRateText = infoGo.transform.Find("winRate/num")?.GetComponent<TMP_Text>() ?? FindChild(infoGo, "winRate")?.transform.Find("num")?.GetComponent<TMP_Text>();
                    _winCntText = infoGo.transform.Find("winCnt/num")?.GetComponent<TMP_Text>() ?? FindChild(infoGo, "winCnt")?.transform.Find("num")?.GetComponent<TMP_Text>();
                }
            }
            else
            {
                // Fallback: StartPanel 不作为独立节点存在时，直接从根节点找
                _realStartBtn = FindChild(gameObject, "StartBtn")?.GetComponent<Button>() ?? transform.Find("StartBtn")?.GetComponent<Button>();
                _offlineStartBtn = FindChild(gameObject, "OfflineStartBtn")?.GetComponent<Button>();
                GameObject infoGo = FindChild(gameObject, "Info") ?? transform.Find("Info")?.gameObject;
                if (infoGo != null)
                {
                    _onlineCntText = infoGo.transform.Find("onlineCnt/num")?.GetComponent<TMP_Text>() ?? FindChild(infoGo, "onlineCnt")?.transform.Find("num")?.GetComponent<TMP_Text>();
                    _winRateText = infoGo.transform.Find("winRate/num")?.GetComponent<TMP_Text>() ?? FindChild(infoGo, "winRate")?.transform.Find("num")?.GetComponent<TMP_Text>();
                    _winCntText = infoGo.transform.Find("winCnt/num")?.GetComponent<TMP_Text>() ?? FindChild(infoGo, "winCnt")?.transform.Find("num")?.GetComponent<TMP_Text>();
                }
            }

            // User Profile
            if (_userProfile != null)
            {
                _userProfileBtn = _userProfile.GetComponent<Button>();
                _nicknameText = _userProfile.Find("Info/Nickname/nickname")?.GetComponent<TMP_Text>() ?? FindChild(_userProfile.gameObject, "nickname")?.GetComponent<TMP_Text>();
                _uidText = _userProfile.Find("Info/UID/UID")?.GetComponent<TMP_Text>() ?? FindChild(_userProfile.gameObject, "title")?.GetComponent<TMP_Text>();
            }

            // Create Player Panel
            if (_createPlayerPanel != null)
            {
                Transform container = _createPlayerPanel.Find("container") ?? FindChild(_createPlayerPanel.gameObject, "container")?.transform;
                if (container != null)
                {
                    _createRoleBtn = container.Find("CreateBtn")?.GetComponent<Button>() ?? FindChild(container.gameObject, "CreateBtn")?.GetComponent<Button>();
                    _createRoleInput = container.Find("Input")?.GetComponentInChildren<TMP_InputField>(true) ?? FindChild(container.gameObject, "Input")?.GetComponentInChildren<TMP_InputField>(true);
                }
                _createPlayerPanel.gameObject.SetActive(false);
            }

            // Cache positions
            CachePosition(_title);
            CachePosition(_closeBtn);
            CachePosition(_backBtn);
            CachePosition(_userProfile);
            if (_startPanel != null)
            {
                CachePosition(_startPanel);
            }
            else
            {
                if (_realStartBtn != null) CachePosition(_realStartBtn.GetComponent<RectTransform>());
                RectTransform infoRect = FindChild(gameObject, "Info")?.GetComponent<RectTransform>();
                if (infoRect != null) CachePosition(infoRect);
            }

            BindEvents();
            AddButtonScales();
        }

        private void CachePosition(RectTransform rect)
        {
            if (rect != null && !_originalPositions.ContainsKey(rect))
            {
                _originalPositions[rect] = rect.anchoredPosition;
            }
        }

        private void AddButtonScales()
        {
            Button[] allBtns = GetComponentsInChildren<Button>(true);
            foreach (var b in allBtns)
            {
                if (b.gameObject.GetComponent<UIButtonScale>() == null)
                {
                    b.gameObject.AddComponent<UIButtonScale>();
                }
            }
        }

        private GameObject FindChild(GameObject root, string name)
        {
            if (root == null) return null;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        private void BindEvents()
        {
            if (_closeBtn != null)
            {
                Button btn = _closeBtn.GetComponentInChildren<Button>(true);
                if (btn != null) btn.onClick.AddListener(OnCloseClicked);
            }

            if (_backBtn != null)
            {
                Button btn = _backBtn.GetComponentInChildren<Button>(true);
                if (btn != null) btn.onClick.AddListener(OnCloseClicked);
            }

            if (_realStartBtn != null)
            {
                _realStartBtn.onClick.AddListener(OnStartMatchClicked);
            }

            if (_offlineStartBtn != null)
            {
                _offlineStartBtn.onClick.AddListener(OnOfflineStartClicked);
            }

            if (_userProfileBtn != null)
            {
                _userProfileBtn.onClick.AddListener(OnUserProfileClicked);
            }

            if (_createRoleBtn != null)
            {
                _createRoleBtn.onClick.RemoveAllListeners();
                _createRoleBtn.onClick.AddListener(() => {
                    Debug.Log("[LobbyPanel] 触发了 CreateRoleBtn 原生点击事件！");
                    OnCreateRoleClicked();
                });
            }
        }

        #region Slide Animation

        private void PlaySlideAnim(RectTransform rt, SlideDir dir, bool isEnter)
        {
            if (rt == null) return;
            if (!_originalPositions.TryGetValue(rt, out Vector2 origPos)) return;

            Vector2 startPos = origPos;
            float offset = 1000f;
            
            switch (dir)
            {
                case SlideDir.Top: startPos.y += offset; break;
                case SlideDir.Bottom: startPos.y -= offset; break;
                case SlideDir.Left: startPos.x -= offset; break;
                case SlideDir.Right: startPos.x += offset; break;
            }

            if (isEnter)
            {
                rt.anchoredPosition = startPos;
                StartCoroutine(LerpPos(rt, startPos, origPos, 0.3f));
            }
            else
            {
                StartCoroutine(LerpPos(rt, origPos, startPos, 0.2f));
            }
        }

        private IEnumerator LerpPos(RectTransform rt, Vector2 start, Vector2 end, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (rt != null) rt.anchoredPosition = Vector2.Lerp(start, end, t / duration);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = end;
        }

        public enum SlideDir { Top, Bottom, Left, Right }

        #endregion

        #region Panel Lifecycle

        public override void OnOpen(object data = null)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            
            // Slide in components
            PlaySlideAnim(_title, SlideDir.Top, true);
            PlaySlideAnim(_closeBtn, SlideDir.Top, true);
            PlaySlideAnim(_backBtn, SlideDir.Top, true);
            PlaySlideAnim(_userProfile, SlideDir.Left, true);

            if (_startPanel != null)
            {
                PlaySlideAnim(_startPanel, SlideDir.Right, true);
            }
            else
            {
                if (_realStartBtn != null) PlaySlideAnim(_realStartBtn.GetComponent<RectTransform>(), SlideDir.Right, true);
                RectTransform infoRect = FindChild(gameObject, "Info")?.GetComponent<RectTransform>();
                if (infoRect != null) PlaySlideAnim(infoRect, SlideDir.Right, true);
            }

            // 订阅大厅数据更新事件
            if (KiHan.Network.LobbyManager.Instance != null)
            {
                KiHan.Network.LobbyManager.Instance.OnPlayerDataUpdated += OnPlayerDataUpdated;
                KiHan.Network.LobbyManager.Instance.OnCreateRoleRequired += OnCreateRoleRequired;
                KiHan.Network.LobbyManager.Instance.OnCreateRoleResponse += OnCreateRoleResponse;
                KiHan.Network.LobbyManager.Instance.OnOnlineCountUpdated += UpdateOnlineCount;
                KiHan.Network.LobbyManager.Instance.OnMatchNtfReceived += OnMatchGameNtf;
                KiHan.Network.LobbyManager.Instance.OnMatchCancelResponse += OnMatchCancelResponse;

                // 如果已经有缓存数据，直接刷新一次
                if (KiHan.Network.LobbyManager.Instance.MyPlayerInfo != null)
                {
                    OnPlayerDataUpdated(KiHan.Network.LobbyManager.Instance.MyPlayerInfo);
                }

                // 主动拉取在线人数
                KiHan.Network.LobbyManager.Instance.RequestGetOnlineCount();

                // 处理竞态条件：在打开面板前就已经收到了创建角色通知
                if (KiHan.Network.LobbyManager.Instance.NeedsCreateRole)
                {
                    OnCreateRoleRequired();
                }
            }
        }

        public override void OnClose()
        {
            if (KiHan.Network.LobbyManager.Instance != null)
            {
                KiHan.Network.LobbyManager.Instance.OnPlayerDataUpdated -= OnPlayerDataUpdated;
                KiHan.Network.LobbyManager.Instance.OnCreateRoleRequired -= OnCreateRoleRequired;
                KiHan.Network.LobbyManager.Instance.OnCreateRoleResponse -= OnCreateRoleResponse;
                KiHan.Network.LobbyManager.Instance.OnOnlineCountUpdated -= UpdateOnlineCount;
                KiHan.Network.LobbyManager.Instance.OnMatchNtfReceived -= OnMatchGameNtf;
                KiHan.Network.LobbyManager.Instance.OnMatchCancelResponse -= OnMatchCancelResponse;
            }

            if (_matchPanelGo != null)
            {
                Destroy(_matchPanelGo);
                _matchPanelGo = null;
            }

            // 先停止旧协程，再启动关闭动画协程
            StopAllCoroutines();
            StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            PlaySlideAnim(_title, SlideDir.Top, false);
            PlaySlideAnim(_closeBtn, SlideDir.Top, false);
            PlaySlideAnim(_backBtn, SlideDir.Bottom, false);
            PlaySlideAnim(_userProfile, SlideDir.Left, false);

            if (_startPanel != null)
            {
                PlaySlideAnim(_startPanel, SlideDir.Right, false);
            }
            else
            {
                if (_realStartBtn != null) PlaySlideAnim(_realStartBtn.GetComponent<RectTransform>(), SlideDir.Right, false);
                RectTransform infoRect = FindChild(gameObject, "Info")?.GetComponent<RectTransform>();
                if (infoRect != null) PlaySlideAnim(infoRect, SlideDir.Right, false);
            }

            yield return new WaitForSecondsRealtime(0.3f);
            gameObject.SetActive(false);
        }

        #endregion

        #region Data Callbacks

        [global::System.Serializable]
        private class PlayerDataJson
        {
            public int total_battle_count;
            public int win_count;
        }

        private void OnPlayerDataUpdated(KiHan.Network.PlayerInfo info)
        {
            Debug.Log($"[LobbyPanel] update player Data");
            UpdateUserProfile(info.nickname, info.uid);

            if (!string.IsNullOrEmpty(info.data_json))
            {
                try
                {
                    var data = JsonUtility.FromJson<PlayerDataJson>(info.data_json);
                    if (data != null)
                    {
                        int total = data.total_battle_count;
                        int win = data.win_count;
                        UpdateWinCount(win);

                        if (total > 0)
                        {
                            int winRate = Mathf.RoundToInt((float)win / total * 100f);
                            UpdateWinRate($"{winRate}%");
                        }
                        else
                        {
                            UpdateWinRate("0%");
                        }
                    }
                    else
                    {
                        UpdateWinCount(0);
                        UpdateWinRate("0%");
                    }
                }
                catch (global::System.Exception e)
                {
                    Debug.LogError($"[LobbyPanel] 解析玩家数据失败: {e.Message}");
                    UpdateWinCount(0);
                    UpdateWinRate("0%");
                }
            }
            else
            {
                UpdateWinCount(0);
                UpdateWinRate("0%");
            }
        }

        private void OnCreateRoleRequired()
        {
            Debug.Log("[LobbyPanel] 接收到创角要求，弹出创角面板");
            if (_createPlayerPanel != null)
            {
                _createPlayerPanel.gameObject.SetActive(true);
                _createPlayerPanel.SetAsLastSibling();
                UIPanelAnim.Show(this, _createPlayerPanel.gameObject);
            }
        }

        private void OnCreateRoleResponse(int errCode)
        {
            Debug.Log($"[LobbyPanel] 接收到创角结果: {errCode}");
            if (errCode == 0)
            {
                if (_createPlayerPanel != null)
                {
                    UIPanelAnim.Hide(this, _createPlayerPanel.gameObject);
                }
            }
            else
            {
                Debug.LogError($"[LobbyPanel] 创角失败，错误码: {errCode}");
                UIManager.Instance.ShowTip($"创角失败({errCode})");
            }
        }

        private void UpdateOnlineCount(int count)
        {
            Debug.Log($"[LobbyPanel] get online count: {count}");
            if (_onlineCntText != null) _onlineCntText.text = count.ToString();
        }

        #endregion

        #region Button Handlers

        private void OnCloseClicked()
        {
            Debug.Log("[LobbyPanel] Close clicked. Showing confirmation.");
            if (_backPanel != null)
            {
                _backPanel.SetActive(true);
                ShowPopup(_backPanel);
            }
            else
            {
                OnRealLogout();
            }
        }

        private void OnOfflineStartClicked()
        {
            Debug.Log("[LobbyPanel] OfflineStartBtn clicked.");
            GameApp.Instance.StartOfflineGame();
        }

        private void OnStartMatchClicked()
        {
            Debug.Log("[LobbyPanel] StartMatch clicked.");
            
            // 暂定角色ID为1
            KiHan.Network.LobbyManager.Instance.RequestMatch(1);
            
            // 实例化匹配面板
            if (_matchPanelGo == null)
            {
                var prefab = Resources.Load<GameObject>("UI/Lobby/MatchPanel");
                if (prefab != null)
                {
                    // MatchPanel 自带 Canvas，直接生成到场景根节点，不要嵌套到 LobbyPanel 下
                    _matchPanelGo = Instantiate(prefab);
                    _matchPanelGo.transform.localScale = Vector3.one;
                    _matchPanelGo.transform.localPosition = Vector3.zero;

                    // 确保 MatchPanel 的 Canvas 层级高于 LobbyPanel，显示在最上层
                    Canvas matchCanvas = _matchPanelGo.GetComponent<Canvas>();
                    if (matchCanvas != null)
                    {
                        // 强制 Overlay 模式，避免编辑器/打包行为不一致
                        matchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        matchCanvas.sortingOrder = 900;
                    }
                    
                    var container = _matchPanelGo.transform.Find("container");
                    if (container != null)
                    {
                        _matchInfoText = container.Find("Info/name")?.GetComponent<TMP_Text>();
                        _matchWaitTimeText = container.Find("WaitInfo/WaitTime/num")?.GetComponent<TMP_Text>();
                        _matchLoadingAnimator = container.Find("loading")?.GetComponent<Animator>();

                        if (_matchInfoText != null) _matchInfoText.text = "正在寻找旗鼓相当的对手...";
                        if (_matchWaitTimeText != null) _matchWaitTimeText.text = "0秒";

                        Button cancelBtn = container.Find("CancelBtn")?.GetComponent<Button>();
                        cancelBtn?.onClick.AddListener(OnCancelMatchClicked);
                    }
                }
            }

            if (_matchPanelGo != null)
            {
                _matchPanelGo.SetActive(true);
                UIPanelAnim.Show(this, _matchPanelGo);
                
                _matchWaitTime = 0f;
                if (_matchTimerCoroutine != null) StopCoroutine(_matchTimerCoroutine);
                _matchTimerCoroutine = StartCoroutine(MatchTimerRoutine());
            }
        }

        private void OnCancelMatchClicked()
        {
            KiHan.Network.LobbyManager.Instance.CancelMatch();
        }

        private void OnUserProfileClicked()
        {
            Debug.Log("[LobbyPanel] UserProfile clicked.");
        }

        private void OnCreateRoleClicked()
        {
            if (_createRoleInput != null && !string.IsNullOrEmpty(_createRoleInput.text.Trim()))
            {
                string nickname = _createRoleInput.text.Trim();
                Debug.Log($"[LobbyPanel] Confirm create role with name: {nickname}");
                UIManager.Instance.ShowTip("正在创建角色...");
                KiHan.Network.LobbyManager.Instance?.RequestCreateRole(nickname);
            }
            else
            {
                UIManager.Instance.ShowTip("昵称不能为空！");
            }
        }

        #endregion

        #region Match Logic

        private IEnumerator MatchTimerRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                _matchWaitTime += 1f;
                if (_matchWaitTimeText != null)
                {
                    _matchWaitTimeText.text = $"{Mathf.FloorToInt(_matchWaitTime)}秒";
                }
            }
        }

        private void OnMatchCancelResponse(int errCode, bool success)
        {
            if (success)
            {
                if (_matchTimerCoroutine != null) StopCoroutine(_matchTimerCoroutine);
                if (_matchPanelGo != null)
                {
                    UIPanelAnim.Hide(this, _matchPanelGo);
                    StartCoroutine(WaitAndDestroyMatchPanel(0.3f));
                }
            }
        }

        private IEnumerator WaitAndDestroyMatchPanel(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_matchPanelGo != null)
            {
                Destroy(_matchPanelGo);
                _matchPanelGo = null;
            }
        }

        private void OnMatchGameNtf(KiHan.Network.MatchGameNtf ntf)
        {
            if (ntf.err_code == 0)
            {
                if (_matchTimerCoroutine != null) StopCoroutine(_matchTimerCoroutine);

                if (_matchInfoText != null) _matchInfoText.text = "匹配成功，正在进入战斗...";
                if (_matchLoadingAnimator != null) _matchLoadingAnimator.Play("loaded");

                // 立即启动战斗初始化（注册 LockstepManager 回调），不能等 UI 动画
                if (uint.TryParse(ntf.room_id, out uint roomId))
                {
                    BattleManager.Instance.EnterBattle(true, roomId);
                }

                // UI 清理延迟执行，不影响核心逻辑
                StartCoroutine(CleanupMatchPanelRoutine());
            }
            else
            {
                Debug.LogError($"[LobbyPanel] Match failed: {ntf.err_code}");
                OnCancelMatchClicked();
            }
        }

        private IEnumerator CleanupMatchPanelRoutine()
        {
            yield return new WaitForSeconds(1.0f);
            
            if (_matchPanelGo != null)
            {
                Destroy(_matchPanelGo);
                _matchPanelGo = null;
            }
        }

        #endregion

        #region Data Updaters

        public void UpdateWinRate(string rateStr)
        {
            if (_winRateText != null) _winRateText.text = rateStr;
        }

        public void UpdateWinCount(int count)
        {
            if (_winCntText != null) _winCntText.text = $"{count}场";
        }

        public void UpdateUserProfile(string nickname, uint uid)
        {
            if (_nicknameText != null) _nicknameText.text = nickname;
            if (_uidText != null) _uidText.text = uid.ToString();
        }

        #endregion

        #region Popup & Logout

        private void ShowPopup(GameObject popup)
        {
            UIPanelAnim.Show(this, popup);
        }

        private void HidePopup(GameObject popup)
        {
            UIPanelAnim.Hide(this, popup);
        }

        private void OnRealLogout()
        {
            GameApp.Instance.PerformTransitionAsync(LogoutRoutine());
        }

        private IEnumerator LogoutRoutine()
        {
            KiHan.Network.GatewayManager.Instance?.Disconnect();
            HttpClient.Instance.ClearToken();
            
            SceneManager.Instance.InitWorld();
            MapManager.Instance.ClearMap();
            if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();
            Managers.ViewManager.Instance.ClearAll();
            Managers.ResManager.Instance.Clear();

            yield return new WaitForSecondsRealtime(0.5f);

            UIManager.Instance.ClosePanel(UIConst.LobbyPanel);
            UIManager.Instance.OpenPanel<LoginPanel>(UIConst.LoginPanel);
        }

        #endregion
    }
}