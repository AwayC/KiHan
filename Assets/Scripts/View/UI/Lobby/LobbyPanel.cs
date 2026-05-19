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

        [Header("Start Panel Info")]
        private Button _realStartBtn;
        private TMP_Text _onlineCntText;
        private TMP_Text _winRateText;
        private TMP_Text _winCntText;

        [Header("User Profile Info")]
        private Button _userProfileBtn;
        private TMP_Text _nicknameText;
        private TMP_Text _uidText;

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

            // If StartPanel is missing (based on prefab text, it might just be the button and Info group directly)
            // Let's bind the StartBtn and Info dynamically
            if (_startPanel == null)
            {
                // Fallback to finding them in root if StartPanel group doesn't exist
                _realStartBtn = FindChild(gameObject, "StartBtn")?.GetComponent<Button>();
                GameObject infoGo = FindChild(gameObject, "Info");
                if (infoGo != null)
                {
                    _onlineCntText = FindChild(infoGo, "onlineCnt")?.GetComponentInChildren<TMP_Text>();
                    _winRateText = FindChild(infoGo, "winRate")?.GetComponentInChildren<TMP_Text>();
                    _winCntText = FindChild(infoGo, "winCnt")?.GetComponentInChildren<TMP_Text>();
                }
            }

            // User Profile
            if (_userProfile != null)
            {
                _userProfileBtn = _userProfile.GetComponent<Button>();
                _nicknameText = _userProfile.Find("Info/Nickname/nickname")?.GetComponent<TMP_Text>();
                _uidText = _userProfile.Find("Info/UID/UID")?.GetComponent<TMP_Text>();
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
        }

        public override void OnClose()
        {
            StopAllCoroutines();
            StartCoroutine(CloseRoutine());
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
            Debug.Log("[LobbyPanel] 开始匹配接口预留...");
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