using UnityEngine;
using UnityEngine.EventSystems;
using Managers;
using KiHan.Logic;

namespace View.UI
{
    /// <summary>
    /// 虚拟按键组件
    /// </summary>
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("对应协议中的 ButtonMask 位")]
        public ButtonMask Action;

        private Vector3 _originalScale;
        private bool _hasInitScale = false;

        private void Start()
        {
            InitScale();
        }

        public void InitScale()
        {
            if (_hasInitScale) return;
            _originalScale = transform.localScale;
            _hasInitScale = true;
        }

        /// <summary>
        /// 动态更换按钮图标
        /// </summary>
        public void SetIcon(Sprite sp)
        {
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.sprite = sp;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, true);
            
            // 按下缩小效果
            transform.localScale = _originalScale * 0.9f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, false);
            
            // 恢复原始缩放
            transform.localScale = _originalScale;
        }
    }
}
