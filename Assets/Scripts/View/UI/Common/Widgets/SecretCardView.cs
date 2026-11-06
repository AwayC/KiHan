using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 秘卷卡片 (通用部件，预制体 UI/Widgets/Common/SecretCard)：圆底 + 秘卷图标
    /// 只负责显示；点击、选中由使用方 (如 SecretSelectItemView) 处理
    /// </summary>
    public partial class SecretCardView : UIView
    {
        public void Bind(SecretScrollEntry scroll)
        {
            _icon.sprite = scroll != null ? scroll.icon : null;
            _icon.enabled = _icon.sprite != null;
        }
    }
}
