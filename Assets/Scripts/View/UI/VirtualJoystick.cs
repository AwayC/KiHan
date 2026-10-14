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

                // 物理死区 (Dead Zone) 过滤：经过摇杆中心时极小的坐标会让方向剧烈抖动
                if (dist > 15f)
                {
                    // 量化为 8 方向：以正右为 0 逆时针每 45° 一档 (Atan2 为负时 & 7 取模)
                    int sector = Mathf.RoundToInt(Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg / 45f) & 7;
                    InputManager.Instance.SetVirtualJoystick((byte)sector);
                }
                else
                {
                    InputManager.Instance.SetVirtualJoystick(JoystickDir.None); // 在死区内视为无输入
                }
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

            InputManager.Instance.SetVirtualJoystick(JoystickDir.None);
        }
    }
}
