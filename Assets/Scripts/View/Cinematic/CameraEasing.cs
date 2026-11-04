using KiHan.Logic;
using UnityEngine;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 镜头缓动曲线求值：输入归一化进度 t ∈ [0, 1]，输出 [0, 1] (BackOut 中途会略超过 1)
    /// </summary>
    public static class CameraEasing
    {
        private const float BackOvershoot = 1.70158f;

        public static float Evaluate(CameraEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case CameraEase.EaseIn:
                    return t * t;
                case CameraEase.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case CameraEase.EaseInOut:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case CameraEase.BackOut:
                {
                    float u = t - 1f;
                    return 1f + (BackOvershoot + 1f) * u * u * u + BackOvershoot * u * u;
                }
                default:
                    return t;
            }
        }
    }
}
