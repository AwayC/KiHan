using System;
using UnityEngine;

namespace KiHan.View.Effects
{
    [Serializable]
    public struct FxColorStep
    {
        public Color color;
        public float duration;
    }

    /// <summary>
    /// 原版 KHChangeColor 的还原：延迟后依次过渡到列表中的各颜色 (每段在 duration 秒内从上一色插值到本色)，
    /// 结果乘到材质颜色上，播完保持最后一色
    /// </summary>
    public sealed class FxChangeColor : FxAnimatedBase
    {
        public float delay;
        public FxColorStep[] steps = Array.Empty<FxColorStep>();

        protected override void Sample(float time)
        {
            FxRendererState state = State;
            if (state == null || steps.Length == 0) return;

            float local = time - delay;
            Color previous = steps[0].color;
            Color color = previous;
            foreach (FxColorStep step in steps)
            {
                if (local <= 0f) break;
                float t = step.duration > 0f ? Mathf.Clamp01(local / step.duration) : 1f;
                color = Color.Lerp(previous, step.color, t);
                local -= step.duration;
                previous = step.color;
            }
            state.ChangeTint = color;
        }
    }
}
