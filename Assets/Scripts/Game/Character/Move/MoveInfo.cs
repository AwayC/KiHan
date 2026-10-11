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
        /// 最终总移动位移向量 (定点数，x 横向, y 纵深；z 不参与位移，竖直方向只走速度模型)
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

        /// <summary>
        /// 按施力者朝向将本地位移 (x 为面朝右时的前方) 转为世界位移
        /// </summary>
        public MoveInfo ToWorld(bool facingLeft)
        {
            MoveInfo world = this;
            if (facingLeft) world.moveDistance.x = -moveDistance.x;
            return world;
        }

        public static MoveInfo Zero => new MoveInfo
        {
            moveDistance = FPVector3.zero,
            inSec = FP.Zero,
            tweenMethod = string.Empty
        };
    }
}
