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
        public float ScaleFactor = 1.2f; // 按下时的背景缩放比例

        private Vector2 _startKnobPos;
        private RectTransform _bgRect;
        private Vector3 _originalBgScale;
        private Vector3 _originalKnobScale;
        private bool _hasInitScale = false;

        private void Start()
        {
            InitScale();
        }

        public void InitScale()
        {
            if (_hasInitScale) return;
            
            _bgRect = GetComponent<RectTransform>();
            _originalBgScale = transform.localScale;

            if (Knob != null)
            {
                _startKnobPos = Knob.anchoredPosition;
                _originalKnobScale = Knob.localScale;
            }
            _hasInitScale = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // 按下时背景变大
            transform.localScale = _originalBgScale * ScaleFactor;

            // 补偿：让 Knob 的视觉大小保持不变 (Knob 的 scale * 父级 scale = 常数)
            if (Knob != null)
            {
                Knob.localScale = _originalKnobScale / ScaleFactor;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Knob == null) return;

            Vector2 localPos;
            // 注意：因为父物体缩放了，坐标转换依然在 _bgRect 本地空间进行
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
            // 松开后恢复背景缩放
            transform.localScale = _originalBgScale;

            // 恢复 Knob 缩放
            if (Knob != null)
            {
                Knob.localScale = _originalKnobScale;
                Knob.anchoredPosition = _startKnobPos;
            }

            InputManager.Instance.SetVirtualJoystick(255); // 停止输入
        }
    }

}
