using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 忍者卡片 (通用部件，预制体 UI/Widgets/Common/NinjaCard)：头像框 + 头像，头像裁切在框内 (HeadshotMask 的 RectMask2D)
    /// 只负责显示；点击、选中由使用方 (如 CharSelectItemView) 处理
    /// </summary>
    public partial class NinjaCardView : UIView
    {
        public void Bind(CharacterManifest manifest)
        {
            _headshot.sprite = manifest != null ? manifest.headshot : null;
            _headshot.enabled = _headshot.sprite != null;
        }
    }
}
