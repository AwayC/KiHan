using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 密码框显示 / 隐藏切换 (登录、注册弹窗共用)：隐藏时显示 open 图标，显示时显示 close 图标
    /// </summary>
    internal static class PasswordVisibility
    {
        public static void Apply(TMPro.TMP_InputField input, GameObject openIcon, GameObject closeIcon, bool visible)
        {
            input.contentType = visible ? TMPro.TMP_InputField.ContentType.Standard : TMPro.TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
            openIcon.SetActive(!visible);
            closeIcon.SetActive(visible);
        }
    }
}
