using System;
using KiHan.Config;
using UnityEngine;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 忍者列表的一项：忍者卡片 + 选中框 (呼吸灯)；整项可点击 (根节点 Button，按压回弹由 UIManager 自动挂上)
    /// 列表项会被复用，选中状态由使用方按下标记录，每次绑定时传入：
    /// - 单选 (训练场换人)：Bind，选中框用预制体里的图
    /// - 按顺序多选 (3v3 选人)：BindOrdered，选中框换成出战序号图 (_orderNumbers 的第 order - 1 张)
    /// </summary>
    public partial class CharSelectItemView : UIView
    {
        [Tooltip("整项的点击按钮 (根节点；点到卡片图片时事件冒泡到这里)")]
        [SerializeField] private Button _button;

        [Tooltip("按顺序多选时的序号图 (1、2、3 ...)；单选时不用")]
        [SerializeField] private UISpriteSet _orderNumbers;

        private Action<int> _onClick;
        private int _index;

        private void Awake()
        {
            _button.onClick.AddListener(OnClicked);
        }

        public void Bind(CharacterManifest manifest, int index, bool selected, Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;
            _card.Bind(manifest);
            SetSelected(selected);
        }

        /// <summary>
        /// order：出战序号 (从 1 起)，0 为未选
        /// </summary>
        public void BindOrdered(CharacterManifest manifest, int index, int order, Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;
            _card.Bind(manifest);
            if (order > 0 && _orderNumbers != null) _selected.sprite = _orderNumbers.Get(order - 1);
            SetSelected(order > 0);
        }

        private void SetSelected(bool selected)
        {
            GameObject selectedGo = _selected.gameObject;
            if (selectedGo.activeSelf != selected) selectedGo.SetActive(selected);
        }

        private void OnClicked()
        {
            _onClick?.Invoke(_index);
        }
    }
}
