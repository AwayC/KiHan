using System;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 面板显示 / 隐藏动画 (挂在面板根节点，UIManager 打开 / 关闭时播放)：
    /// 缩放 + 淡入淡出、或从屏幕边滑入；按 unscaledDeltaTime 推进 (不受暂停影响)，不使用协程，空闲时不更新
    /// 透明度通过 CanvasGroup 控制 (同节点没有时自动添加)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITransition : MonoBehaviour
    {
        public enum Kind
        {
            Scale,
            Fade,
            SlideLeft,
            SlideRight,
            SlideTop,
            SlideBottom,
        }

        [SerializeField] private Kind _kind = Kind.Scale;
        [SerializeField] private float _showDuration = 0.2f;
        [SerializeField] private float _hideDuration = 0.15f;

        [Tooltip("缩放动画的起始缩放")]
        [SerializeField] private float _fromScale = 0.8f;

        [Tooltip("滑入距离 (参考分辨率像素)")]
        [SerializeField] private float _slideDistance = 600f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private bool _showing;
        private float _elapsed;
        private float _duration;
        private Action _onComplete;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _restPosition = _rect.anchoredPosition;
            enabled = false;
        }

        /// <summary>
        /// 播放显示 (show) 或隐藏动画，结束时调用 onComplete；打断上一次未播完的动画 (直接从当前状态切换)
        /// </summary>
        public void Play(bool show, Action onComplete)
        {
            _showing = show;
            _onComplete = onComplete;
            _elapsed = 0f;
            _duration = Mathf.Max(0.0001f, show ? _showDuration : _hideDuration);
            _group.blocksRaycasts = show;
            Apply(show ? 0f : 1f);
            enabled = true;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            Apply(_showing ? EaseOut(t) : 1f - t);
            if (t < 1f) return;

            enabled = false;
            Action done = _onComplete;
            _onComplete = null;
            done?.Invoke();
        }

        /// <param name="v">0 = 完全隐藏，1 = 完全显示</param>
        private void Apply(float v)
        {
            _group.alpha = v;
            switch (_kind)
            {
                case Kind.Scale:
                    float s = Mathf.LerpUnclamped(_fromScale, 1f, v);
                    _rect.localScale = new Vector3(s, s, 1f);
                    break;
                case Kind.Fade:
                    break;
                default:
                    _rect.anchoredPosition = _restPosition + SlideOffset() * (1f - v);
                    break;
            }
        }

        private Vector2 SlideOffset()
        {
            switch (_kind)
            {
                case Kind.SlideLeft: return new Vector2(-_slideDistance, 0f);
                case Kind.SlideRight: return new Vector2(_slideDistance, 0f);
                case Kind.SlideTop: return new Vector2(0f, _slideDistance);
                case Kind.SlideBottom: return new Vector2(0f, -_slideDistance);
                default: return Vector2.zero;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
