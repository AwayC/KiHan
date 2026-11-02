using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace KiHan.UI
{
    /// <summary>
    /// 登录主界面：视频背景、未登录 / 已登录两组按钮、版本校验、开始游戏 (连接网关)、退出登录。
    /// 登录 / 注册在 LoginPopup / RegisterPopup 中完成，成功后回调本界面切到已登录状态
    /// </summary>
    [UIPanel("UI/Panels/Login/LoginPanel", UILayer.Normal)]
    public partial class LoginPanel : UIPanel
    {
        private const string VideoFileName = "LoginTen.mp4";
        private const string GatewayHost = "127.0.0.1";
        private const int GatewayPort = 9000;
        private const string TipInvalidVersion = "版本不对，请更新游戏";
        private const string TipNetworkError = "网络问题";

        private VideoPlayer _videoPlayer;
        private AspectRatioFitter _videoFitter;
        private RenderTexture _videoRT;
        private bool _isLoggedIn;

        private LoginPopup.Args _loginArgs;
        private RegisterPopup.Args _registerArgs;

        protected override void OnCreate()
        {
            SetupVideoBackground();

            _loginArgs = new LoginPopup.Args { OnLoggedIn = OnLoggedIn };
            _registerArgs = new RegisterPopup.Args { OnRegistered = OnLoggedIn };

            _loginBtn.onClick.AddListener(() => UIManager.Instance.Open<LoginPopup, LoginPopup.Args>(_loginArgs));
            _registerBtn.onClick.AddListener(() => UIManager.Instance.Open<RegisterPopup, RegisterPopup.Args>(_registerArgs));
            _startBtn.onClick.AddListener(OnStartGameClick);
            _logoutBtn.onClick.AddListener(OnLogoutClick);
        }

        protected override void OnOpen()
        {
            _isLoggedIn = false;
            RefreshState();

            _videoPlayer.Play();
            CheckAppVersion();

            var gateway = KiHan.Network.GatewayManager.Instance;
            if (gateway != null)
            {
                gateway.OnAuthSuccess += OnGatewayAuthSuccess;
                gateway.OnAuthFailed += OnGatewayAuthFailed;
            }
        }

        protected override void OnClose()
        {
            _videoPlayer.Stop();

            var gateway = KiHan.Network.GatewayManager.Instance;
            if (gateway != null)
            {
                gateway.OnAuthSuccess -= OnGatewayAuthSuccess;
                gateway.OnAuthFailed -= OnGatewayAuthFailed;
            }
        }

        protected override void OnRelease()
        {
            if (_videoRT != null)
            {
                _videoPlayer.targetTexture = null;
                _videoBg.texture = null;
                _videoRT.Release();
                Destroy(_videoRT);
                _videoRT = null;
            }
        }

        /// <summary>
        /// 视频背景：节点 (RawImage + AspectRatioFitter + VideoPlayer) 在预制体里，这里只创建 RenderTexture 并配置播放
        /// </summary>
        private void SetupVideoBackground()
        {
            _videoBg.color = Color.white;
            _videoBg.raycastTarget = false;

            // 填满且不拉伸：视频完整拉伸到 RenderTexture，由 AspectRatioFitter 按视频原始比例还原
            _videoFitter = _videoBg.GetComponent<AspectRatioFitter>();
            _videoFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _videoFitter.aspectRatio = 16f / 9f;

            _videoRT = new RenderTexture(1920, 1080, 0);
            _videoBg.texture = _videoRT;

            _videoPlayer = _videoBg.GetComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _videoRT;
            _videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, VideoFileName);
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            if (vp.height > 0) _videoFitter.aspectRatio = (float)vp.width / vp.height;
        }

        private void CheckAppVersion()
        {
            Managers.HttpClient.Instance.CheckVersion(KiHan.Config.HttpConfig.AppVersion, res =>
            {
                if (res.code == (int)KiHan.Config.HttpErrCode.InvalidVersion)
                {
                    UIManager.Instance.Toast(TipInvalidVersion);
                }
            }, err =>
            {
                Debug.LogWarning($"[LoginPanel] 版本校验请求失败(网络问题): {err}");
                UIManager.Instance.Toast(TipNetworkError);
            });
        }

        private void OnLoggedIn()
        {
            _isLoggedIn = true;
            RefreshState();
        }

        private void RefreshState()
        {
            // 未登录时显示
            _unloginGroup.SetActive(!_isLoggedIn);
            _centerBtns.SetActive(!_isLoggedIn);
            _rights.SetActive(!_isLoggedIn);

            // 登录后显示
            _loginedGroup.SetActive(_isLoggedIn);
            _startBtns.SetActive(_isLoggedIn);
        }

        private void OnStartGameClick()
        {
            // HTTP 登录缓存下来的 Token
            string token = Managers.HttpClient.Instance.Token;
            if (string.IsNullOrEmpty(token))
            {
                UIManager.Instance.Toast("没有找到 Token，请重新登录！");
                return;
            }

            // 本地测试先写死网关地址；鉴权结果由 OnGatewayAuthSuccess / OnGatewayAuthFailed 处理
            KiHan.Network.GatewayManager.Instance.Connect(GatewayHost, GatewayPort, token);
        }

        private void OnLogoutClick()
        {
            _isLoggedIn = false;
            RefreshState();
            GameFlow.Instance.ResetSession();
        }

        private void OnGatewayAuthSuccess()
        {
            UIManager.Instance.Toast("连接网关成功！");
            GameFlow.Instance.EnterLobby();
        }

        private void OnGatewayAuthFailed(string msg)
        {
            UIManager.Instance.Toast("连接网关失败: " + msg);
        }
    }
}
