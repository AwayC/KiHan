using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 豆的显示状态
    /// </summary>
    public enum BeanState
    {
        /// <summary>
        /// 不显示 (超出当前上限)：只关掉图片，节点保持激活，仍占据布局位置，排版间距与编辑器中一致
        /// </summary>
        Hidden,

        Off,
        On,

        /// <summary>
        /// 满豆 (亮的颗数达到满豆阈值)：有满豆图时显示满豆图，否则同 On
        /// </summary>
        Full,
    }

    /// <summary>
    /// 一颗豆 (查克拉 / 替身 / 通灵)：灭、亮、满三张图同一时刻最多显示一张，Hidden 时全部不显示
    /// </summary>
    public class LightIcon : MonoBehaviour
    {
        [SerializeField] private Image _off;
        [SerializeField] private Image _on;

        [Tooltip("满豆时显示的图 (可不填，不填时满豆显示亮图)")]
        [SerializeField] private Image _full;

        private BeanState _state;
        private bool _applied;

        public void SetState(BeanState state)
        {
            if (_applied && _state == state) return;
            _state = state;
            _applied = true;

            bool full = state == BeanState.Full && _full != null;
            _off.enabled = state == BeanState.Off;
            _on.enabled = (state == BeanState.On || state == BeanState.Full) && !full;
            if (_full != null) _full.enabled = full;
        }
    }
}
