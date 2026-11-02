using UnityEngine;
using UnityEngine.EventSystems;
using Managers;
using KiHan.Logic;

namespace View.UI
{
    /// <summary>
    /// 虚拟按键组件：按下 / 松开写入 InputManager；按压的 Q 弹缩放由同节点的 UIPressBounce 负责 (没有时自动添加，挂了 UIPressBounceIgnore 则不加)
    /// </summary>
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("对应协议中的 ButtonMask 位")]
        public ButtonMask Action;

        private void Awake()
        {
            if (!TryGetComponent(out UIPressBounce _) && !TryGetComponent(out UIPressBounceIgnore _)) gameObject.AddComponent<UIPressBounce>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InputManager.Instance.SetVirtualButton(Action, false);
        }
    }
}
