using UnityEngine;
using UnityEngine.EventSystems;
using Managers;
using KiHan.Logic;

namespace View.UI
{
    /// <summary>
    /// 虚拟摇杆组件
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("配置")]
        public RectTransform Knob; // 摇杆头
        public float MaxRadius = 100f; // 最大拖拽半径

        private Vector2 _startPos;
        private RectTransform _bgRect;

        private void Start()
        {
            _bgRect = GetComponent<RectTransform>();
            _startPos = Knob.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 localPos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bgRect, eventData.position, eventData.pressEventCamera, out localPos))
            {
                // 限制半径
                float dist = Vector2.Distance(Vector2.zero, localPos);
                if (dist > MaxRadius)
                {
                    localPos = localPos.normalized * MaxRadius;
                }

                Knob.anchoredPosition = localPos;

                // 计算角度并发送给 InputManager
                float angle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360;
                
                InputManager.Instance.SetVirtualJoystick((byte)(angle / 2));
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Knob.anchoredPosition = _startPos;
            InputManager.Instance.SetVirtualJoystick(255); // 停止输入
        }
    }
}
