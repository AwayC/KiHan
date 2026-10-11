using System;
using UnityEngine;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 攻击对目标命中的状态记录 (纯定点数确定性计时)
    /// 值类型，存放于 CharacterObj.HitRecords 列表中按下标原地更新，0 GC
    /// </summary>
    public struct HitRecord
    {
        /// <summary>
        /// 打中的目标逻辑实体 ID (CharacterObj.EntityId，确定性)
        /// </summary>
        public int UniqueId;

        /// <summary>
        /// 这是第几段伤害 (对应 AttackInfo.phase)
        /// </summary>
        public int Phase;

        /// <summary>
        /// 还能命中该目标几次
        /// </summary>
        public int CanHitTimes;

        /// <summary>
        /// 剩余冷却时间 (定点数秒，如 0.2s)
        /// </summary>
        public FP Cooldown;

        public HitRecord(CharacterObj cha, int phase, int canHitTimes, FP cooldown)
        {
            UniqueId = cha != null ? cha.EntityId : 0;
            Phase = phase;
            CanHitTimes = canHitTimes;
            Cooldown = cooldown;
        }

        /// <summary>
        /// 当前是否允许再次命中该目标
        /// </summary>
        public bool CanHitNow => CanHitTimes > 0 && Cooldown <= FP.Zero;

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
