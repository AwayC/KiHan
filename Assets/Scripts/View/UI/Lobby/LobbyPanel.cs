using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Managers;

namespace KiHan.View.UI.Lobby
{
    public class LobbyPanel : BasePanel
    {
        [Header("Components")]
        private RectTransform _title;
        private RectTransform _closeBtn;
        private RectTransform _backBtn;
        private RectTransform _startPanel;
        private RectTransform _userProfile;
        private RectTransform _createPlayerPanel;

        [Header("Start Panel Info")]
        private Button _realStartBtn;
        private TMP_Text _onlineCntText;
        private TMP_Text _winRateText;
        private TMP_Text _winCntText;

        [Header("User Profile Info")]
        private Button _userProfileBtn;
        private TMP_Text _nicknameText;
        private TMP_Text _uidText;

        [Header("Create Player Info")]
        private Button _createRoleBtn;
        private TMP_InputField _createRoleInput;

        // Cache original positions
        private Dictionary<RectTransform, Vector2> _originalPositions = new Dictionary<RectTransform, Vector2>();

        private void Awake()
        {
            // Bind RectTransforms for animation
            // 优先查找根节点的直接子物体，防止和深层节点的同名物体（如 nickname/title）冲突
            _title = transform.Find("Title")?.GetComponent<RectTransform>() ?? transform.Find("title")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "Title")?.GetComponent<RectTransform>();
            _closeBtn = transform.Find("CloseBtn")?.GetComponent<RectTransform>() ?? transform.Find("xBtn")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "CloseBtn")?.GetComponent<RectTransform>();
            _backBtn = transform.Find("BackBtn")?.GetComponent<RectTransform>() ?? transform.Find("backBtn")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "backBtn")?.GetComponent<RectTransform>();
            _startPanel = transform.Find("StartPanel")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "StartPanel")?.GetComponent<RectTransform>();
            _userProfile = transform.Find("UserProfile")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "UserProfile")?.GetComponent<RectTransform>();
            _createPlayerPanel = transform.Find("CreatePlayerPanel")?.GetComponent<RectTransform>() ?? FindChild(gameObject, "CreatePlayerPanel")?.GetComponent<RectTransform>();

            // If StartPanel is missing (based on prefab text, it might just be the button and Info group directly)
            // Let's bind the StartBtn and Info dynamically
            if (_startPanel == null)
            {
                // Fallback to finding them in root if StartPanel group doesn't exist
                _realStartBtn = FindChild(gameObject, "StartBtn")?.GetComponent<Button>() ?? transform.Find("StartBtn")?.GetComponent<Button>();
                GameObject infoGo = FindChild(gameObject, "Info") ?? transform.Find("Info")?.gameObject;
                if (infoGo != null)
                {
                    _onlineCntText = FindChild(infoGo, "onlineCnt")?.GetComponentInChildren<TMP_Text>(true) ?? FindChild(infoGo, "name")?.GetComponentInChildren<TMP_Text>(true); // online人数在预制体里叫name
                    _winRateText = FindChild(infoGo, "winRate")?.GetComponentInChildren<TMP_Text>(true) ?? infoGo.transform.Find("winRate/num")?.GetComponent<TMP_Text>();
                    _winCntText = FindChild(infoGo, "winCnt")?.GetComponentInChildren<TMP_Text>(true) ?? infoGo.transform.Find("winCnt/name")?.GetComponent<TMP_Text>(); // 胜场数
                }
            }
            else
            {
                _realStartBtn = _startPanel.Find("StartBtn")?.GetComponent<Button>() ?? FindChild(_startPanel.gameObject, "StartBtn")?.GetComponent<Button>();
                GameObject infoGo = _startPanel.Find("Info")?.gameObject ?? FindChild(_startPanel.gameObject, "Info");
                if (infoGo != null)
                {
                    _onlineCntText = FindChild(infoGo, "onlineCnt")?.GetComponentInChildren<TMP_Text>(true) ?? FindChild(infoGo, "name")?.GetComponentInChildren<TMP_Text>(true);
                    _winRateText = FindChild(infoGo, "winRate")?.GetComponentInChildren<TMP_Text>(true) ?? infoGo.transform.Find("winRate/num")?.GetComponent<TMP_Text>();
                    _winCntText = FindChild(infoGo, "winCnt")?.GetComponentInChildren<TMP_Text>(true) ?? infoGo.transform.Find("winCnt/name")?.GetComponent<TMP_Text>();
                }
            }

            // User Profile
            if (_userProfile != null)
            {
                _userProfileBtn = _userProfile.GetComponent<Button>();
                _nicknameText = _userProfile.Find("Info/Nickname/nickname")?.GetComponent<TMP_Text>() ?? FindChild(_userProfile.gameObject, "nickname")?.GetComponent<TMP_Text>();
                _uidText = _userProfile.Find("Info/UID/UID")?.GetComponent<TMP_Text>() ?? FindChild(_userProfile.gameObject, "title")?.GetComponent<TMP_Text>(); // 预制体里UID叫title
            }

            // Create Player Panel
            if (_createPlayerPanel != null)
            {
                Debug.Log("[LobbyPanel] 成功找到 CreatePlayerPanel");
                
                Transform container = _createPlayerPanel.Find("container") ?? FindChild(_createPlayerPanel.gameObject, "container")?.transform;
                if (container != null)
                {
                    _createRoleBtn = container.Find("CreateBtn")?.GetComponent<Button>() ?? FindChild(container.gameObject, "CreateBtn")?.GetComponent<Button>();
                    _createRoleInput = container.Find("Input")?.GetComponentInChildren<TMP_InputField>(true) ?? FindChild(container.gameObject, "Input")?.GetComponentInChildren<TMP_InputField>(true);
                }

                if (_createRoleBtn == null) Debug.LogError("[LobbyPanel] 错误：未找到 CreateBtn 组件！");
                else Debug.Log("[LobbyPanel] 成功找到 CreateBtn 组件！");

                if (_createRoleInput == null) Debug.LogError("[LobbyPanel] 错误：未找到 TMP_InputField 组件！");
                else Debug.Log("[LobbyPanel] 成功找到 TMP_InputField 组件！");

                _createPlayerPanel.gameObject.SetActive(false); // 默认隐藏
            }
            else
            {
                Debug.LogError("[LobbyPanel] 错误：根本没有找到 CreatePlayerPanel！");
            }

            // Cache positions
            CachePosition(_title);
            CachePosition(_closeBtn);
            CachePosition(_backBtn);
            CachePosition(_userProfile);
            
            // For StartBtn and Info if StartPanel doesn't exist as a single group
            if (_startPanel != null) CachePosition(_startPanel);
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

                // 如果已经有缓存数据，直接刷新一次
                if (KiHan.Network.LobbyManager.Instance.MyPlayerInfo != null)
                {
                    OnPlayerDataUpdated(KiHan.Network.LobbyManager.Instance.MyPlayerInfo);
                }

                // 处理可能发生的竞态条件：在打开面板前就已经收到了创建角色通知或登录时返回了-2202无角色错误
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
            }

            StopAllCoroutines();
            StartCoroutine(CloseRoutine());
        }

        [global::System.Serializable]
        private class PlayerDataJson
        {
            public int total_battle_count;
            public int win_count;
        }

        private void OnPlayerDataUpdated(KiHan.Network.PlayerInfo info)
        {
            UpdateUserProfile(info.nickname, $"UID:{info.uid}");

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
                    Debug.LogError($"[LobbyPanel] Parse data_json error: {e.Message}");
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
            Debug.Log("[LobbyPanel] Showing Create Player Panel.");
            if (_createPlayerPanel != null)
            {
                _createPlayerPanel.gameObject.SetActive(true);
                _createPlayerPanel.SetAsLastSibling(); // 确保在最上层，防止被挡住点击不到
            }
        }

        private void OnCreateRoleResponse(int errCode)
        {
            if (errCode == 0)
            {
                Debug.Log("[LobbyPanel] Create Role Success.");
                if (_createPlayerPanel != null)
                {
                    _createPlayerPanel.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogError($"[LobbyPanel] Create Role Failed with code: {errCode}");
                // TODO: 可以在界面上提示错误，比如弹个Toast
            }
        }

        private void OnCreateRoleClicked()
        {
            if (_createRoleInput != null)
            {
                string nickname = _createRoleInput.text.Trim();
                if (string.IsNullOrEmpty(nickname))
                {
                    UIManager.Instance.ShowTip("昵称不能为空！");
                    return;
                }
                
                // 给个点击后的UI提示反馈
                UIManager.Instance.ShowTip("正在创建角色...");
                KiHan.Network.LobbyManager.Instance?.RequestCreateRole(nickname);
            }
            else
            {
                UIManager.Instance.ShowTip("UI组件绑定失败，找不到输入框");
            }
        }

        private IEnumerator CloseRoutine()
        {
            // Slide out components
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

            // Wait for animation to finish (duration is 0.3f by default in UISlideAnim)
            yield return new WaitForSecondsRealtime(0.3f);
            gameObject.SetActive(false);
        }

        private void PlaySlideAnim(RectTransform target, SlideDir dir, bool isShow)
        {
            if (target != null && _originalPositions.TryGetValue(target, out Vector2 origPos))
            {
                StartCoroutine(UISlideAnim.DoSlide(target, origPos, dir, isShow, 0.5f, 800f));
            }
        }

        private void OnCloseClicked()
        {
            UIManager.Instance.ClosePanel(UIConst.LobbyPanel);
            UIManager.Instance.OpenPanel<KiHan.View.UI.Login.LoginPanel>(UIConst.LoginPanel);
        }

        private void OnStartMatchClicked()
        {
            Debug.Log("[LobbyPanel] Start Offline Game...");
            // 单机测试：直接调用 GameApp 的单机启动逻辑
            GameApp.Instance.StartOfflineGame();
        }

        private void OnUserProfileClicked()
        {
            Debug.Log("[LobbyPanel] 打开用户信息面板接口预留...");
        }

        // --- 供外部调用的数据刷新接口 ---

        public void UpdateOnlineCount(int count)
        {
            if (_onlineCntText != null) _onlineCntText.text = count.ToString();
        }

        public void UpdateWinRate(string rateStr)
        {
            if (_winRateText != null) _winRateText.text = rateStr;
        }

        public void UpdateWinCount(int count)
        {
            if (_winCntText != null) _winCntText.text = count.ToString();
        }

        public void UpdateUserProfile(string nickname, string uid)
        {
            if (_nicknameText != null) _nicknameText.text = nickname;
            if (_uidText != null) _uidText.text = uid;
        }
    }
}