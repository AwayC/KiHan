using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 血条：当前血量立即到位；白血显示一段连续伤害的总量 (格斗游戏惯例)
    /// - 受击时白血停在这段连续伤害开始前的血量，恢复不透明并立刻在 fadeSeconds 秒内渐变透明
    /// - 完全透明前再次受击：重置为不透明并继续叠加，再重新渐隐；完全透明后白血归位到当前血量，下一次受击开始新的一段
    /// 血条图保持原样不伸缩，每条图套一层 RectMask2D，按血量调节遮罩的裁切边距 (矩形蒙版效果)
    /// 注意：RectMask2D 本身不能被镜像 (世界空间绕 Y 轴 180°)，否则裁切矩形宽度为负、整条被裁掉；
    /// 需要镜像的血条 (P2) 让遮罩节点反向旋转抵消父节点的镜像，再让遮罩内的血条图自己镜像
    /// </summary>
    public class HpBarView : MonoBehaviour
    {
        [Tooltip("当前血量图外层的遮罩")]
        [SerializeField] private RectMask2D _fillMask;

        [Tooltip("白血图外层的遮罩")]
        [SerializeField] private RectMask2D _delayMask;

        [Tooltip("白血图 (渐隐时修改其透明度)")]
        [SerializeField] private Graphic _delayImage;

        [Tooltip("扣血时固定屏幕右端、从左边裁切 (P2)；不勾选则固定屏幕左端、从右边裁切 (P1)。遮罩边距按画布空间计算，与节点是否镜像无关")]
        [SerializeField] private bool _anchorRight;

        [Tooltip("白血从不透明渐变到完全透明的时长 (秒)：期间受到的伤害都叠加进同一段白血")]
        [SerializeField] private float _fadeSeconds = 0.2f;

        private float _target = 1f;
        private float _shown = 1f;

        private float _fadeElapsed;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>
        /// 立即设置 (开局 / 重置时用，不显示白血)
        /// </summary>
        public void SetImmediate(float ratio)
        {
            _target = Mathf.Clamp01(ratio);
            _shown = _target;
            _fadeElapsed = 0f;
            _delayImage.canvasRenderer.SetAlpha(1f);
            Apply(_fillMask, _target);
            Apply(_delayMask, _target);
            enabled = false;
        }

        public void SetRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (Mathf.Approximately(ratio, _target)) return;

            if (ratio > _target)
            {
                // 回血：白血直接跟到新血量
                _shown = Mathf.Max(_shown, ratio);
                Apply(_delayMask, _shown);
            }
            else
            {
                // 掉血：白血不动 (叠加本段连续伤害)，重置为不透明后重新渐隐
                _fadeElapsed = 0f;
                _delayImage.canvasRenderer.SetAlpha(1f);
                enabled = true;
            }
            _target = ratio;
            Apply(_fillMask, ratio);
        }

        private void Update()
        {
            _fadeElapsed += Time.unscaledDeltaTime;
            if (_fadeElapsed < _fadeSeconds)
            {
                _delayImage.canvasRenderer.SetAlpha(1f - _fadeElapsed / _fadeSeconds);
                return;
            }

            // 渐隐结束：白血归位到当前血量 (被当前血量图盖住)，恢复不透明供下一段伤害使用
            _shown = _target;
            Apply(_delayMask, _shown);
            _delayImage.canvasRenderer.SetAlpha(1f);
            _fadeElapsed = 0f;
            enabled = false;
        }

        /// <summary>
        /// 遮罩裁掉 (1 - ratio) 的宽度：边距 x 为左、z 为右 (画布空间)
        /// </summary>
        private void Apply(RectMask2D mask, float ratio)
        {
            float cut = mask.canvasRect.width * (1f - ratio);
            Vector4 padding = mask.padding;
            if (_anchorRight) padding.x = cut;
            else padding.z = cut;
            mask.padding = padding;
        }
    }
}
