using System;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 注册弹窗：用户名 / 密码 / 确认密码 / 邮箱 HTTP 注册 (注册成功即登录)，成功后关闭并回调 OnRegistered
    /// </summary>
    [UIPanel("UI/Panels/Login/RegisterPopup", UILayer.Popup)]
    public partial class RegisterPopup : UIPanel<RegisterPopup.Args>
    {
        public struct Args
        {
            public Action OnRegistered;
        }

        private Args _args;
        private bool _passwordVisible;
        private bool _confirmPasswordVisible;

        protected override void OnCreate()
        {
            _closeBtn.onClick.AddListener(Close);
            _registerBtn.onClick.AddListener(OnRegisterClick);
            _passwordToggleBtn.onClick.AddListener(() =>
            {
                _passwordVisible = !_passwordVisible;
                PasswordVisibility.Apply(_passwordInput, _passwordOpen, _passwordClose, _passwordVisible);
            });
            _confirmPasswordToggleBtn.onClick.AddListener(() =>
            {
                _confirmPasswordVisible = !_confirmPasswordVisible;
                PasswordVisibility.Apply(_confirmPasswordInput, _confirmPasswordOpen, _confirmPasswordClose, _confirmPasswordVisible);
            });
            PasswordVisibility.Apply(_passwordInput, _passwordOpen, _passwordClose, false);
            PasswordVisibility.Apply(_confirmPasswordInput, _confirmPasswordOpen, _confirmPasswordClose, false);
        }

        protected override void OnOpen(Args args)
        {
            _args = args;
        }

        private void OnRegisterClick()
        {
            var http = Managers.HttpClient.Instance;
            if (!http.IsVersionValid)
            {
                UIManager.Instance.Toast("版本不对，请更新游戏");
                return;
            }

            string username = _usernameInput.text;
            string email = _emailInput.text;
            string password = _passwordInput.text;
            string confirmPassword = _confirmPasswordInput.text;

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
            if (password != confirmPassword)
            {
                UIManager.Instance.Toast("两次输入的密码不一致！");
                return;
            }

            Debug.Log($"[RegisterPopup] 正在注册: {username}, Email: {email}");

            http.Register(username, password, email,
                onSuccess: res =>
                {
                    if (res.code == (int)KiHan.Config.HttpErrCode.Ok)
                    {
                        UIManager.Instance.Toast("注册成功！");
                        Action onRegistered = _args.OnRegistered;
                        _args = default;
                        Close();
                        onRegistered?.Invoke();
                    }
                    else
                    {
                        UIManager.Instance.Toast("注册失败: " + res.msg);
                    }
                },
                onError: err =>
                {
                    Debug.LogWarning($"[RegisterPopup] 请求失败: {err}");
                    UIManager.Instance.Toast("网络问题");
                });
        }
    }
}
