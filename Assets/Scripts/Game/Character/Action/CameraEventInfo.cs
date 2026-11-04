using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 镜头指令名 (Camera 事件的 asset)
    /// - impact / impact_heavy：固定的打击冲击 (放大 5% 一次 / 两次)
    /// - shot：镜头移向事件点并缩放 / 旋转，保持后复位 (可跟随施法者，施法者被打断时自动复位)
    /// - shake：上下左右晃动 (位移噪声，可带旋转抖动)，随时间衰减
    /// - pulse：放大缩小震动 (缩放脉冲，可多次)
    /// - reset：提前结束本玩家的 shot
    /// </summary>
    public static class CameraEffects
    {
        public const string Impact = "impact";
        public const string ImpactHeavy = "impact_heavy";
        public const string Shot = "shot";
        public const string Shake = "shake";
        public const string Pulse = "pulse";
        public const string Reset = "reset";

        public static bool IsKnown(string asset)
        {
            return asset == Impact || asset == ImpactHeavy || asset == Shot || asset == Shake || asset == Pulse || asset == Reset;
        }
    }

    /// <summary>
    /// 镜头过渡的缓动曲线 (表现层求值)
    /// </summary>
    public enum CameraEase
    {
        Linear = 0,
        /// <summary>起步慢收尾快 (二次)</summary>
        EaseIn = 1,
        /// <summary>起步快收尾慢 (二次)</summary>
        EaseOut = 2,
        /// <summary>两头慢中间快 (三次)</summary>
        EaseInOut = 3,
        /// <summary>冲过头再回弹 (夸张推镜)</summary>
        BackOut = 4,
    }

    /// <summary>
    /// Camera 事件参数 (运行时)。只通知表现层，不参与逻辑；按指令取用：
    /// - shot：zoom / roll / inSec / holdSec (holdUntilActionEnd) / outSec / ease
    /// - shake：amplitude / frequency / duration / roll (旋转抖动幅度)
    /// - pulse：zoom / inSec / holdSec / outSec / count / interval / ease
    /// - reset：outSec / ease
    /// </summary>
    [Serializable]
    public struct CameraEventInfo
    {
        /// <summary>正交尺寸倍率：小于 1 推近</summary>
        public FP zoom;

        /// <summary>shot：屏幕内旋转 (度，朝左释放时取反)；shake：旋转抖动幅度 (度)</summary>
        public FP roll;

        public FP inSec;
        public FP holdSec;

        /// <summary>shot 未配置 holdSec：保持到施法者本动作结束</summary>
        public bool holdUntilActionEnd;

        public FP outSec;
        public CameraEase ease;

        /// <summary>shake：位移振幅 (世界单位)</summary>
        public FP amplitude;

        /// <summary>shake：频率 (Hz)</summary>
        public FP frequency;

        /// <summary>shake：持续时间 (秒，振幅线性衰减到 0)</summary>
        public FP duration;

        /// <summary>pulse：次数</summary>
        public int count;

        /// <summary>pulse：两次之间的间隔 (秒)</summary>
        public FP interval;
    }

    /// <summary>
    /// Camera 事件参数 DTO (定点数字符串，未填按指令取默认值)
    /// </summary>
    [Serializable]
    public struct CameraEventData
    {
        [Tooltip("shot / pulse：正交尺寸倍率，< 1 推近 (shot 默认 1，pulse 默认 0.95)")]
        public string zoom;

        [Tooltip("shot：屏幕内旋转 (度)；shake：旋转抖动幅度 (度)")]
        public string roll;

        [Tooltip("shot / pulse：进入时间 (秒)")]
        public string inSec;

        [Tooltip("shot：保持时间 (秒)，不填则保持到本动作结束；pulse：每次保持时间")]
        public string holdSec;

        [Tooltip("shot / pulse / reset：复位时间 (秒)")]
        public string outSec;

        [Tooltip("Linear / EaseIn / EaseOut / EaseInOut / BackOut (默认 EaseOut)")]
        public string ease;

        [Tooltip("shake：位移振幅 (世界单位，默认 0.1)")]
        public string amplitude;

        [Tooltip("shake：频率 (Hz，默认 20)")]
        public string frequency;

        [Tooltip("shake：持续时间 (秒，默认 0.3)")]
        public string duration;

        [Tooltip("pulse：次数 (默认 1)")]
        public int count;

        [Tooltip("pulse：两次之间的间隔 (秒，默认 0.04)")]
        public string interval;

        public CameraEventInfo ToRuntime(string asset)
        {
            bool pulse = asset == CameraEffects.Pulse;
            return new CameraEventInfo
            {
                zoom = Fp(zoom, pulse ? "0.95" : "1"),
                roll = Fp(roll, "0"),
                inSec = Fp(inSec, pulse ? "0" : "0.25"),
                holdSec = Fp(holdSec, pulse ? "0.04" : "0"),
                holdUntilActionEnd = asset == CameraEffects.Shot && string.IsNullOrEmpty(holdSec),
                outSec = Fp(outSec, pulse ? "0" : "0.25"),
                ease = ActionData.ParseEnum(ease, CameraEase.EaseOut),
                amplitude = Fp(amplitude, "0.1"),
                frequency = Fp(frequency, "20"),
                duration = Fp(duration, "0.3"),
                count = count > 0 ? count : 1,
                interval = Fp(interval, "0.04")
            };
        }

        private static FP Fp(string value, string fallback) => ActionData.ParseFP(string.IsNullOrEmpty(value) ? fallback : value);
    }
}
