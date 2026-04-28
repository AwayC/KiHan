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

        public void OnPointerDown(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, true);
            
            // 简单的按下缩放效果
            transform.localScale = Vector3.one * 0.9f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, false);
            
            transform.localScale = Vector3.one;
        }
    }
}
