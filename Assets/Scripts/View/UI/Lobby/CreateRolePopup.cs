using KiHan.Network;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 创建角色弹窗：输入昵称 → LobbyManager.RequestCreateRole，成功后关闭，失败飘字提示
    /// </summary>
    [UIPanel("UI/Panels/Lobby/CreateRolePopup", UILayer.Popup)]
    public partial class CreateRolePopup : UIPanel
    {
        private const string EmptyNicknameTip = "昵称不能为空！";
        private const string CreatingTip = "正在创建角色...";

        protected override void OnCreate()
        {
            _createBtn.onClick.AddListener(OnCreateClicked);
        }

        protected override void OnOpen()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby == null) return;
            lobby.OnCreateRoleResponse -= OnCreateRoleResponse;
            lobby.OnCreateRoleResponse += OnCreateRoleResponse;
        }

        protected override void OnClose()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby != null) lobby.OnCreateRoleResponse -= OnCreateRoleResponse;
        }

        private void OnCreateClicked()
        {
            string nickname = _nicknameInput.text.Trim();
            if (nickname.Length == 0)
            {
                UIManager.Instance.Toast(EmptyNicknameTip);
                return;
            }

            UIManager.Instance.Toast(CreatingTip);
            LobbyManager.Instance?.RequestCreateRole(nickname);
        }

        private void OnCreateRoleResponse(int errCode)
        {
            if (errCode == 0)
            {
                Close();
                return;
            }

            Debug.LogError($"[CreateRolePopup] 创角失败，错误码: {errCode}");
            UIManager.Instance.Toast($"创角失败({errCode})");
        }
    }
}
