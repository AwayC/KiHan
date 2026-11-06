using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 接段窗口倒计时 (技能按键上的圆形进度条，预制体 UI/Widgets/BattleHud/CDTimer)：
    /// 根节点圆框为 Filled / Radial360 / 顶部起点 / 逆时针，剩余比例即 fillAmount，缺口从顶点顺时针扩大；
    /// @point 是进度条末端的点，按角度摆在剩余弧线的终点上
    /// 不在接段窗口时只关闭图片 (不 SetActive，不触发重新排版)；只在比例变化时修改 UI
    /// </summary>
    [RequireComponent(typeof(Image))]
    public partial class SkillWindowView : KiHan.UI.UIView
    {
        private Image _ring;
        private float _radius = -1f;
        private float _shown = -1f;

        /// <param name="ratio">剩余比例 (0~1)；&lt;= 0 时隐藏</param>
        public void Show(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio == _shown) return;
            _shown = ratio;

            if (_ring == null)
            {
                _ring = GetComponent<Image>();
                // 末端点到圆心的距离取预制体里摆放的位置 (顶点处)
                _radius = _point.rectTransform.anchoredPosition.magnitude;
            }

            bool visible = ratio > 0f;
            if (_ring.enabled != visible) _ring.enabled = visible;
            if (_point.enabled != visible) _point.enabled = visible;
            if (!visible) return;

            _ring.fillAmount = ratio;
            // 已消失的角度从顶点顺时针量起，剩余弧线的终点即末端点
            float angle = (1f - ratio) * 2f * Mathf.PI;
            _point.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * _radius;
        }
    }
}
