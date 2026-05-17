using UnityEngine;
using UnityEngine.UI;
using Managers;
using UnityEngine.Video;
using TMPro;

namespace KiHan.View.UI.Login
{
    public class LoginPanel : BasePanel
    {
        // UI 组
        private GameObject _unLoginGroup;
        private GameObject _loginedGroup;
        private GameObject _centerBtns;
        private GameObject _rights;
        private GameObject _startBtns;

        // 弹窗
        private GameObject _loginPopup;
        private GameObject _registerPopup;

        private VideoPlayer _videoPlayer;
        private bool _isLoggedIn = false;

        private void Awake()
        {
            // 查找各个模块（兼容了首字母大小写）
            _unLoginGroup = FindChild(gameObject, "unLoginGroup") ?? FindChild(gameObject, "UnloginGroup");
            _loginedGroup = FindChild(gameObject, "LoginedGroup");
            _centerBtns = FindChild(gameObject, "centerBtns");
            _rights = FindChild(gameObject, "rights");
            _startBtns = FindChild(gameObject, "StartBtns");

            _loginPopup = FindChild(gameObject, "LoginPanel");
            _registerPopup = FindChild(gameObject, "RegisterPanel");

            SetupVideoBackground();
            BindEvents();
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

        private void SetupVideoBackground()
        {
            GameObject bgGo = new GameObject("VideoBackground");
            bgGo.transform.SetParent(this.transform);
            bgGo.transform.SetAsFirstSibling(); 
            
            RectTransform rt = bgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            _videoPlayer = bgGo.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = true;
            _videoPlayer.isLooping = true;
            _videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
            _videoPlayer.targetCameraAlpha = 1f;
            _videoPlayer.aspectRatio = VideoAspectRatio.FitVertically; // 高度对齐屏幕，宽度可能会被裁剪或留黑边
            
            if (Camera.main != null)
            {
                _videoPlayer.targetCamera = Camera.main;
            }
            else
            {
                // 如果场景中没有主相机，动态创建一个供视频渲染使用
                GameObject camGo = new GameObject("LoginCamera");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camGo.tag = "MainCamera";
                _videoPlayer.targetCamera = cam;
            }

            string videoPath = System.IO.Path.Combine(Application.dataPath, "AssetPackages/Movies/LoginTen.mp4");
            _videoPlayer.url = videoPath;
            _videoPlayer.Play();
        }

        private void BindEvents()
        {
            // 给所有按键统一自动加上点击缩放动画组件
            Button[] allBtns = GetComponentsInChildren<Button>(true);
            foreach (var b in allBtns)
            {
                if (b.gameObject.GetComponent<UIButtonScale>() == null)
                {
                    b.gameObject.AddComponent<UIButtonScale>();
                }
            }

            // 1. centerBtns 里的按钮
            if (_centerBtns != null)
            {
                Button loginBtn = FindChild(_centerBtns, "LoginBtn")?.GetComponent<Button>();
                if (loginBtn != null) loginBtn.onClick.AddListener(() => ShowPopup(_loginPopup));

                Button regBtn = FindChild(_centerBtns, "RegisterBtn")?.GetComponent<Button>();
                if (regBtn != null) regBtn.onClick.AddListener(() => ShowPopup(_registerPopup));
            }

            // 2. 登录弹窗 LoginPanel
            if (_loginPopup != null)
            {
                Button xBtn = FindChild(_loginPopup, "XBtn")?.GetComponent<Button>();
                if (xBtn != null) xBtn.onClick.AddListener(() => HidePopup(_loginPopup));

                Button loginBtn = FindChild(_loginPopup, "LoginBtn")?.GetComponent<Button>();
                if (loginBtn != null) loginBtn.onClick.AddListener(OnRealLoginSubmit);
            }

            // 3. 注册弹窗 RegisterPanel
            if (_registerPopup != null)
            {
                Button xBtn = FindChild(_registerPopup, "XBtn")?.GetComponent<Button>();
                if (xBtn != null) xBtn.onClick.AddListener(() => HidePopup(_registerPopup));

                Button regBtn = FindChild(_registerPopup, "RegisterBtn")?.GetComponent<Button>();
                if (regBtn != null) regBtn.onClick.AddListener(OnRealRegisterSubmit);
            }

            // 4. StartBtns 里的开始游戏按钮
            if (_startBtns != null)
            {
                Button startBtn = FindChild(_startBtns, "StartBtn")?.GetComponent<Button>();
                if (startBtn != null) startBtn.onClick.AddListener(OnStartGameSubmit);
            }

            // 5. 退出登录按钮
            if (_loginedGroup != null)
            {
                Button backBtn = FindChild(_loginedGroup, "back")?.GetComponent<Button>();
                if (backBtn != null) backBtn.onClick.AddListener(OnLogoutSubmit);
            }
        }

        public override void OnOpen(object data = null)
        {
            base.OnOpen(data);
            
            // 初始化状态：未登录
            _isLoggedIn = false;
            // 直接设为 false 避免初始状态播放隐藏动画
            if (_loginPopup != null) _loginPopup.SetActive(false);
            if (_registerPopup != null) _registerPopup.SetActive(false);
            RefreshState();

            if (_videoPlayer != null) _videoPlayer.Play();
        }

        public override void OnClose()
        {
            base.OnClose();
            if (_videoPlayer != null) _videoPlayer.Stop();
        }

        private void ShowPopup(GameObject popup)
        {
            UIPanelAnim.Show(this, popup);
        }

        private void HidePopup(GameObject popup)
        {
            UIPanelAnim.Hide(this, popup);
        }

        // 核心状态刷新逻辑
        private void RefreshState()
        {
            // 未登录时显示
            if (_unLoginGroup != null) _unLoginGroup.SetActive(!_isLoggedIn);
            if (_centerBtns != null) _centerBtns.SetActive(!_isLoggedIn);
            if (_rights != null) _rights.SetActive(!_isLoggedIn);

            // 登录后显示
            if (_loginedGroup != null) _loginedGroup.SetActive(_isLoggedIn);
            if (_startBtns != null) _startBtns.SetActive(_isLoggedIn);
        }

        private void OnRealLoginSubmit()
        {
            Debug.Log("[LoginPanel] 点击了登录面板上的登录，切换至已登录状态");
            _isLoggedIn = true;
            HidePopup(_loginPopup);
            RefreshState();
        }

        private void OnRealRegisterSubmit()
        {
            Debug.Log("[LoginPanel] 注册接口预留...");
        }

        private void OnStartGameSubmit()
        {
            Debug.Log("[LoginPanel] 加载Lobby页面接口预留...");
            // TODO: 后续在这里切换到大厅
        }

        private void OnLogoutSubmit()
        {
            Debug.Log("[LoginPanel] 退出登录接口预留...");
            _isLoggedIn = false;
            RefreshState();
        }
    }
}