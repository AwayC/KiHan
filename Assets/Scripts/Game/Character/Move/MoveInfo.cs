using System;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 角色受击/技能强制外力位移配置数据 (基于定点数)
    /// </summary>
    [Serializable]
    public struct MoveInfo
    {
        /// <summary>
        /// 最终总移动位移向量 (定点数)
        /// </summary>
        public FPVector3 moveDistance;

        /// <summary>
        /// 持续时间 (定点数秒，例如 FP.Parse("0.3"))
        /// </summary>
        public FP inSec;

        /// <summary>
        /// 缓动算法名 (从 ForceMoveMethod.Methods 中获取)
        /// </summary>
        public string tweenMethod;

        public static MoveInfo Zero => new MoveInfo
        {
            moveDistance = FPVector3.zero,
            inSec = FP.Zero,
            tweenMethod = string.Empty
        };
    }
}
