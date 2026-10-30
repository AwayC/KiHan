using System;
using UnityEngine;

namespace KiHan.View.Effects
{
    public enum FxCurveType
    {
        Position,
        Rotation,
        Scale,
        Color,
        TextureUV,
        MeshColor,
    }

    [Serializable]
    public class FxCurve
    {
        public FxCurveType type;

        [Tooltip("作用分量：位置/旋转/缩放为 xyz，颜色为 rgba，贴图偏移为 xy")]
        public bool x, y, z, w;

        [Tooltip("曲线值倍率 (颜色类不使用)")]
        public float valueScale = 1f;

        public Color from = Color.white;
        public Color to = Color.white;

        [Tooltip("横轴为归一化时间 0~1")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// 原版 FXMaker NcCurveAnimation 的还原：按归一化时间 (延迟后经过时间 / 时长) 采样各曲线
    /// - 位置 / 旋转 (欧拉角)：原值 + 曲线值 × 倍率
    /// - 缩放：原值 × 曲线值 × 倍率
    /// - 颜色 / 网格颜色：在 from→to 之间按曲线值插值，乘到材质颜色上 (未勾选的通道保持 1)
    /// - 贴图偏移：曲线值 × 倍率
    /// 播完且 autoDestruct 时隐藏本节点及子节点 (原版销毁节点)
    /// </summary>
    public sealed class FxCurveAnimation : FxAnimatedBase
    {
        public float delay;
        public float duration = 1f;
        public bool loop;
        public int loopCount;
        public bool autoDestruct;
        public FxCurve[] curves = Array.Empty<FxCurve>();

        private bool _captured;
        private Vector3 _position;
        private Vector3 _euler;
        private Vector3 _scale;

        private void Capture()
        {
            if (_captured) return;
            _captured = true;
            _position = transform.localPosition;
            _euler = transform.localEulerAngles;
            _scale = transform.localScale;
        }

        protected override void Restart()
        {
            Capture();
            transform.localPosition = _position;
            transform.localEulerAngles = _euler;
            transform.localScale = _scale;
            if (autoDestruct) FxRendererState.SetDestroyed(transform, false);
        }

        protected override void Sample(float time)
        {
            Capture();
            float local = Mathf.Max(0f, time - delay);
            float rate;
            bool finished;
            if (duration <= 0f)
            {
                rate = 1f;
                finished = true;
            }
            else if (loop)
            {
                int cycle = Mathf.FloorToInt(local / duration);
                finished = loopCount > 0 && cycle >= loopCount;
                rate = finished ? 1f : (local - cycle * duration) / duration;
            }
            else
            {
                rate = Mathf.Clamp01(local / duration);
                finished = local >= duration;
            }

            Vector3 position = _position;
            Vector3 euler = _euler;
            Vector3 scale = _scale;
            bool moved = false, rotated = false, scaled = false;
            FxRendererState state = State;
            foreach (FxCurve c in curves)
            {
                float v = c.curve.Evaluate(rate);
                switch (c.type)
                {
                    case FxCurveType.Position:
                        position += Mask(c, v * c.valueScale);
                        moved = true;
                        break;
                    case FxCurveType.Rotation:
                        euler += Mask(c, v * c.valueScale);
                        rotated = true;
                        break;
                    case FxCurveType.Scale:
                        float s = v * c.valueScale;
                        if (c.x) scale.x = _scale.x * s;
                        if (c.y) scale.y = _scale.y * s;
                        if (c.z) scale.z = _scale.z * s;
                        scaled = true;
                        break;
                    case FxCurveType.Color:
                    case FxCurveType.MeshColor:
                        if (state == null) break;
                        Color lerped = Color.LerpUnclamped(c.from, c.to, v);
                        state.CurveTint = new Color(c.x ? lerped.r : 1f, c.y ? lerped.g : 1f, c.z ? lerped.b : 1f, c.w ? Mathf.Clamp01(lerped.a) : 1f);
                        break;
                    case FxCurveType.TextureUV:
                        if (state == null) break;
                        float o = v * c.valueScale;
                        state.CurveOffset = new Vector2(c.x ? o : 0f, c.y ? o : 0f);
                        break;
                }
            }

            if (moved) transform.localPosition = position;
            if (rotated) transform.localEulerAngles = euler;
            if (scaled) transform.localScale = scale;
            if (autoDestruct) FxRendererState.SetDestroyed(transform, finished);
        }

        private static Vector3 Mask(FxCurve c, float v)
        {
            return new Vector3(c.x ? v : 0f, c.y ? v : 0f, c.z ? v : 0f);
        }
    }
}
