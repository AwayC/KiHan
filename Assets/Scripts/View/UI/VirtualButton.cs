using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        private bool _isScaleInit = false;

        private void Start()
        {
            EnsureScaleInit();
        }

        public void EnsureScaleInit()
        {
            if (_isScaleInit) return;
            _originalScale = transform.localScale;
            _isScaleInit = true;
        }

        public void SetIcon(Sprite sp)
        {
            var img = GetComponent<Image>();
            if (img != null) img.sprite = sp;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureScaleInit();
            //Debug.Log($"<color=yellow>[UI] 点击了按钮: {Action} ({gameObject.name})</color>");
            InputManager.Instance.SetVirtualButton(Action, true);
            transform.localScale = _originalScale * 0.9f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, false);
            transform.localScale = _originalScale;
        }
    }
}
