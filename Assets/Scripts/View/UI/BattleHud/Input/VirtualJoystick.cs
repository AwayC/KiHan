using UnityEngine;
using UnityEngine.EventSystems;
using Managers;
using KiHan.Logic;

namespace View.UI
{
    /// <summary>
    /// 虚拟摇杆：拖拽摇杆头 (@Knob) 在背景 (@Bg) 范围内移动，量化为 8 方向写入 InputManager；按下时背景放大
    /// </summary>
    public partial class VirtualJoystick : KiHan.UI.UIView, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("配置")]
        public float MaxRadius = 100f; // 最大拖拽半径
        public float ScaleFactor = 1.4f; // 按下时的背景缩放比例

        /// <summary>
        /// 死区半径：经过摇杆中心时极小的坐标会让方向剧烈抖动
        /// </summary>
        private const float DeadZone = 15f;

        private Vector2 _startKnobPos;
        private Vector3 _originalBgScale;

        private void Awake()
        {
            _originalBgScale = _bg.localScale;
            _startKnobPos = _knob.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _bg.localScale = _originalBgScale * ScaleFactor;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 判定区域基于背景图空间
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_bg, eventData.position, eventData.pressEventCamera, out Vector2 localPos)) return;

            float dist = localPos.magnitude;
            if (dist > MaxRadius) localPos = localPos / dist * MaxRadius;
            _knob.anchoredPosition = localPos;

            if (dist > DeadZone)
            {
                // 量化为 8 方向：以正右为 0 逆时针每 45° 一档 (Atan2 为负时 & 7 取模)
                int sector = Mathf.RoundToInt(Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg / 45f) & 7;
                InputManager.Instance.SetVirtualJoystick((byte)sector);
            }
            else
            {
                InputManager.Instance.SetVirtualJoystick(JoystickDir.None);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _bg.localScale = _originalBgScale;
            _knob.anchoredPosition = _startKnobPos;
            InputManager.Instance.SetVirtualJoystick(JoystickDir.None);
        }
    }
}
