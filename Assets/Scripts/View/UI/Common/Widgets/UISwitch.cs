using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 开关按钮：点击切换开 / 关，关闭时图片压暗 (预制体里摆放的颜色)，开启时显示原色 (_onColor，默认白色即图片原色)
    /// 挂在带 Image 的节点上；节点命名 @Xxx 时自动绑定为 UISwitch
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class UISwitch : UIView, IPointerClickHandler
    {
        [Tooltip("开启时的颜色 (白色 = 图片原色)")]
        [SerializeField] private Color _onColor = Color.white;

        /// <summary>
        /// 由点击引起的开关变化 (参数为新状态)；SetOn 不触发
        /// </summary>
        public event Action<bool> Changed;

        public bool IsOn { get; private set; }

        private Image _image;
        private Color _offColor;

        private void Awake()
        {
            EnsureInit();
        }

        /// <summary>
        /// 设置开关状态 (只改显示，不触发 Changed)
        /// </summary>
        public void SetOn(bool on)
        {
            EnsureInit();
            IsOn = on;
            _image.color = on ? _onColor : _offColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging) return;
            SetOn(!IsOn);
            Changed?.Invoke(IsOn);
        }

        private void EnsureInit()
        {
            if (_image != null) return;
            _image = GetComponent<Image>();
            _offColor = _image.color;
        }
    }
}
