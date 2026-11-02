using System;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 登录弹窗：用户名 / 密码 HTTP 登录，成功后关闭并回调 OnLoggedIn
    /// </summary>
    [UIPanel("UI/Panels/Login/LoginPopup", UILayer.Popup)]
    public partial class LoginPopup : UIPanel<LoginPopup.Args>
    {
        public struct Args
        {
            public Action OnLoggedIn;
        }

        private Args _args;
        private bool _passwordVisible;

        protected override void OnCreate()
        {
            _closeBtn.onClick.AddListener(Close);
            _loginBtn.onClick.AddListener(OnLoginClick);
            _passwordToggleBtn.onClick.AddListener(() =>
            {
                _passwordVisible = !_passwordVisible;
                PasswordVisibility.Apply(_passwordInput, _passwordOpen, _passwordClose, _passwordVisible);
            });
            PasswordVisibility.Apply(_passwordInput, _passwordOpen, _passwordClose, false);
        }

        protected override void OnOpen(Args args)
        {
            _args = args;
        }

        private void OnLoginClick()
        {
            var http = Managers.HttpClient.Instance;
            if (!http.IsVersionValid)
            {
                UIManager.Instance.Toast("版本不对，请更新游戏");
                return;
            }

            string username = _usernameInput.text;
            string password = _passwordInput.text;

            if (string.IsNullOrEmpty(username))
            {
                UIManager.Instance.Toast("用户名不能为空！");
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                UIManager.Instance.Toast("密码不能为空！");
                return;
            }

            Debug.Log($"[LoginPopup] 正在登录: {username}");

            http.Login(username, password,
                onSuccess: res =>
                {
                    if (res.code == (int)KiHan.Config.HttpErrCode.Ok)
                    {
                        UIManager.Instance.Toast("登录成功！");
                        Action onLoggedIn = _args.OnLoggedIn;
                        _args = default;
                        Close();
                        onLoggedIn?.Invoke();
                    }
                    else
                    {
                        UIManager.Instance.Toast("登录失败: " + res.msg);
                    }
                },
                onError: err =>
                {
                    Debug.LogWarning($"[LoginPopup] 请求失败: {err}");
                    UIManager.Instance.Toast("网络问题");
                });
        }
    }
}
