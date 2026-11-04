using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 按住型按钮：按下 / 松开各通知一次 (Button.onClick 只在松开时触发，按住型操作用本组件)，按住期间图片显示 _pressedColor。
    /// 长按锁定 (LatchSeconds &gt; 0)：按住满该秒数后松手仍保持按下 (常亮)，再点一次解除。
    /// 禁用时若仍按着则自动松开；节点命名 @Xxx 时自动绑定为 UIHoldButton
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class UIHoldButton : UIView, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("按住时的颜色 (白色 = 图片原色)")]
        [SerializeField] private Color _pressedColor = Color.white;

        [Tooltip("长按锁定所需秒数 (0 = 不锁定，松手即松开)")]
        [SerializeField] private float _latchSeconds;

        /// <summary>
        /// 按下 (true) / 松开 (false)
        /// </summary>
        public event Action<bool> PressChanged;

        public bool IsPressed { get; private set; }

        /// <summary>
        /// 已长按锁定 (松手后仍保持按下)
        /// </summary>
        public bool IsLatched { get; private set; }

        public float LatchSeconds
        {
            get => _latchSeconds;
            set => _latchSeconds = value;
        }

        private Image _image;
        private Color _normalColor;
        private bool _pointerDown;
        private bool _unlatchOnUp;
        private float _downTime;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _normalColor = _image.color;
        }

        private void OnDisable()
        {
            _pointerDown = false;
            _unlatchOnUp = false;
            IsLatched = false;
            SetPressed(false);
        }

        private void Update()
        {
            if (!_pointerDown || IsLatched || _latchSeconds <= 0f) return;
            if (Time.unscaledTime - _downTime >= _latchSeconds) IsLatched = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pointerDown = true;
            if (IsLatched)
            {
                // 已锁定：这次点击用于解除，松手时松开
                _unlatchOnUp = true;
                return;
            }
            _downTime = Time.unscaledTime;
            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pointerDown = false;
            if (_unlatchOnUp)
            {
                _unlatchOnUp = false;
                IsLatched = false;
                SetPressed(false);
            }
            else if (!IsLatched)
            {
                SetPressed(false);
            }
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed) return;
            IsPressed = pressed;
            if (_image != null) _image.color = pressed ? _pressedColor : _normalColor;
            PressChanged?.Invoke(pressed);
        }
    }
}
