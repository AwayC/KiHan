using System;
using UnityEngine;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 攻击对目标命中的状态记录 (纯定点数确定性计时)
    /// </summary>
    public class HitRecord
    {
        /// <summary>
        /// 打中的目标实例唯一 ID
        /// </summary>
        public int UniqueId;

        /// <summary>
        /// 这是第几段伤害 (对应 AttackInfo.phase)
        /// </summary>
        public int Phase = 0;

        /// <summary>
        /// 还能命中该目标几次
        /// </summary>
        public int CanHitTimes = 0;

        /// <summary>
        /// 剩余冷却时间 (定点数秒，如 0.2s)
        /// </summary>
        public FP Cooldown = FP.Zero;

        public HitRecord(CharacterObj cha, int phase, int canHitTimes, FP cooldown)
        {
            UniqueId = cha != null ? cha.gameObject.GetInstanceID() : 0;
            Phase = phase;
            CanHitTimes = canHitTimes;
            Cooldown = cooldown;
        }

        /// <summary>
        /// 逻辑帧驱动：默认推进一个逻辑帧长 (GameConfig.LOGIC_TICK_TIME_FP)
        /// </summary>
        public void Tick() => Tick(GameConfig.LOGIC_TICK_TIME_FP);

        public void Tick(FP deltaFP)
        {
            if (Cooldown > FP.Zero)
            {
                Cooldown -= deltaFP;
                if (Cooldown < FP.Zero) Cooldown = FP.Zero;
            }
        }
    }
}
