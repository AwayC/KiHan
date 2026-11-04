using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 呼吸灯：透明度在 [_minAlpha, 1] 之间按正弦往复 (如列表选中框)；节点隐藏时不运行，重新显示时从最亮开始
    /// 用 CanvasRenderer.SetAlpha，不改 Graphic.color (不触发网格重建)；按 unscaledDeltaTime 推进
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UIBreathe : MonoBehaviour
    {
        [Tooltip("最暗时的透明度")]
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.35f;

        [Tooltip("一呼一吸的周期 (秒)")]
        [SerializeField] private float _period = 1.2f;

        private CanvasRenderer _renderer;
        private float _time;

        private void Awake()
        {
            _renderer = GetComponent<CanvasRenderer>();
        }

        private void OnEnable()
        {
            _time = 0f;
            _renderer.SetAlpha(1f);
        }

        private void OnDisable()
        {
            _renderer.SetAlpha(1f);
        }

        private void Update()
        {
            if (_period <= 0f) return;
            _time = (_time + Time.unscaledDeltaTime) % _period;
            // cos 从 1 开始：显示时先亮后暗
            float k = 0.5f + 0.5f * Mathf.Cos(_time / _period * 2f * Mathf.PI);
            _renderer.SetAlpha(Mathf.Lerp(_minAlpha, 1f, k));
        }
    }
}
