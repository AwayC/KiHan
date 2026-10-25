using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Managers
{
    /// <summary>
    /// 按键 Q 弹缩放 (基础组件)：按下缩小，松开弹回原大小并小幅过冲 (大 → 小 → 回稳)
    /// - 用阻尼弹簧模拟 (欠阻尼产生回弹)，只改自身 localScale，静止后不再计算 (0 GC)
    /// - 同节点有 Selectable (Button 等) 且不可交互时不播放
    /// - BasePanel 在 Awake 时给面板内所有 Button 自动挂上；VirtualButton 自带；其余节点可直接手动添加
    /// - 有特殊按压设计的按钮挂 UIPressBounceIgnore，自动挂载时跳过
    /// 组件需保持启用：EventSystem 不向禁用的组件派发点击事件
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("按下时的缩放倍数 (相对原始缩放)")]
        [SerializeField] private float _pressedScale = 0.88f;

        [Tooltip("弹簧刚度：越大回弹越快越硬")]
        [SerializeField] private float _stiffness = 700f;

        [Tooltip("阻尼：越小回弹次数越多、幅度越大 (约 2×√刚度 时完全不回弹)")]
        [SerializeField] private float _damping = 16f;

        private const float SubStep = 1f / 120f;
        private const float SettleOffset = 0.0005f;
        private const float SettleSpeed = 0.005f;

        private Selectable _selectable;
        private Vector3 _baseScale;
        private float _value = 1f;
        private float _velocity;
        private float _goal = 1f;
        private bool _pressed;
        private bool _animating;

        /// <summary>
        /// 给 root 下 (含未激活节点) 所有 Button 挂上本组件；已有本组件或挂了 UIPressBounceIgnore 的跳过
        /// </summary>
        public static void AttachToButtons(GameObject root)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.TryGetComponent(out UIPressBounce _) || button.TryGetComponent(out UIPressBounceIgnore _)) continue;
                button.gameObject.AddComponent<UIPressBounce>();
            }
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
            TryGetComponent(out _selectable);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            _pressed = true;
            _goal = _pressedScale;
            _animating = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pressed) Release();
        }

        private void Release()
        {
            _pressed = false;
            _goal = 1f;
            _animating = true;
        }

        private void Update()
        {
            if (!_animating) return;

            // 固定小步长积分：帧率波动时手感一致且不发散
            float remaining = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (remaining > 0f)
            {
                float h = Mathf.Min(SubStep, remaining);
                float accel = _stiffness * (_goal - _value) - _damping * _velocity;
                _velocity += accel * h;
                _value += _velocity * h;
                remaining -= h;
            }

            bool moving = Mathf.Abs(_goal - _value) > SettleOffset || Mathf.Abs(_velocity) > SettleSpeed;
            if (!moving)
            {
                _value = _goal;
                _velocity = 0f;
            }
            transform.localScale = _baseScale * _value;
            // 按住期间保持运行；松开且静止后停止
            _animating = moving || _pressed;
        }

        private void OnDisable()
        {
            // 运动中被隐藏：恢复原大小，避免下次显示时停在缩小状态
            _pressed = false;
            _animating = false;
            _goal = 1f;
            _value = 1f;
            _velocity = 0f;
            transform.localScale = _baseScale;
        }
    }
}
