using UnityEngine;
using UnityEngine.EventSystems;
using Managers;
using KiHan.Logic;

namespace View.UI
{
    /// <summary>
    /// 虚拟摇杆组件
    /// 脚本挂在 EmptyObj 上，通过引用控制子物体 Bg 和 Knob
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("配置")]
        public RectTransform Bg;   // 背景图 (用于缩放)
        public RectTransform Knob; // 摇杆头
        public float MaxRadius = 100f; // 最大拖拽半径
        public float ScaleFactor = 1.4f; // 按下时的背景缩放比例

        private Vector2 _startKnobPos;
        private Vector3 _originalBgScale;
        private bool _hasInitScale = false;

        private void Start()
        {
            InitScale();
        }

        public void InitScale()
        {
            if (_hasInitScale) return;
            
            if (Bg != null)
            {
                _originalBgScale = Bg.localScale;
            }
            else
            {
                _originalBgScale = Vector3.one;
            }

            if (Knob != null)
            {
                _startKnobPos = Knob.anchoredPosition;
            }
            _hasInitScale = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // 核心修复：按下时仅放大背景图
            if (Bg != null)
            {
                Bg.localScale = _originalBgScale * ScaleFactor;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Knob == null || Bg == null) return;

            Vector2 localPos;
            // 判定区域基于背景图空间
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Bg, eventData.position, eventData.pressEventCamera, out localPos))
            {
                float dist = Vector2.Distance(Vector2.zero, localPos);
                if (dist > MaxRadius)
                {
                    localPos = localPos.normalized * MaxRadius;
                }

                Knob.anchoredPosition = localPos;

                float angle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360;
                InputManager.Instance.SetVirtualJoystick((byte)(angle / 2));
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 还原背景缩放
            if (Bg != null)
            {
                Bg.localScale = _originalBgScale;
            }

            // 还原位置
            if (Knob != null)
            {
                Knob.anchoredPosition = _startKnobPos;
            }

            InputManager.Instance.SetVirtualJoystick(255);
        }
    }
}
