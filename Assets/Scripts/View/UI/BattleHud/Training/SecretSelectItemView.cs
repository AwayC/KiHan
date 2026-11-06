using System;
using KiHan.Config;
using UnityEngine;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 换人弹窗秘卷页的一项：秘卷卡片 + 选中框 (呼吸灯)；整项可点击 (根节点 Button，按压回弹由 UIManager 自动挂上)
    /// 列表项会被复用，选中状态由 CharSelectPopup 按下标记录，每次绑定时传入
    /// </summary>
    public partial class SecretSelectItemView : UIView
    {
        [Tooltip("整项的点击按钮 (根节点；点到卡片图片时事件冒泡到这里)")]
        [SerializeField] private Button _button;

        private Action<int> _onClick;
        private int _index;

        private void Awake()
        {
            _button.onClick.AddListener(OnClicked);
        }

        public void Bind(SecretScrollEntry scroll, int index, bool selected, Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;
            _card.Bind(scroll);
            GameObject selectedGo = _selected.gameObject;
            if (selectedGo.activeSelf != selected) selectedGo.SetActive(selected);
        }

        private void OnClicked()
        {
            _onClick?.Invoke(_index);
        }
    }
}
