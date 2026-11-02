using System;

namespace KiHan.UI
{
    /// <summary>
    /// 通用确认弹窗 (UIManager.Confirm)：确认 / 取消 / 右上角关闭 (等同取消)
    /// </summary>
    [UIPanel("UI/Panels/Common/ConfirmPopup", UILayer.Popup)]
    public partial class ConfirmPopup : UIPanel<ConfirmPopup.Args>
    {
        public struct Args
        {
            public string Text;
            public Action OnConfirm;
            public Action OnCancel;
        }

        private Args _args;

        protected override void OnCreate()
        {
            _confirmBtn.onClick.AddListener(() => Finish(_args.OnConfirm));
            _cancelBtn.onClick.AddListener(() => Finish(_args.OnCancel));
            _closeBtn.onClick.AddListener(() => Finish(_args.OnCancel));
        }

        protected override void OnOpen(Args args)
        {
            _args = args;
            _text.text = args.Text ?? string.Empty;
        }

        private void Finish(Action callback)
        {
            _args = default;
            Close();
            callback?.Invoke();
        }
    }
}
