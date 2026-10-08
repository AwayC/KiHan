using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 造成的伤害与命中硬直/击退信息 (基于定点数)
    /// </summary>
    [Serializable]
    public struct AttackInfo
    {
        /// <summary>
        /// 段数 (第几段伤害)
        /// </summary>
        public int phase;

        /// <summary>
        /// 伤害值或伤害倍率 (定点数)
        /// </summary>
        public FP attack;

        /// <summary>
        /// 判定方向 (向前/向后/向上/向下)
        /// </summary>
        public ForceDirection forceDir;

        /// <summary>
        /// 推动力 (基于定点数的 ForceMove 物理位移)
        /// </summary>
        public MoveInfo pushPower;

        /// <summary>
        /// 目标受击硬直时间 (定点数秒，如 0.2s)
        /// </summary>
        public FP hitStun;

        /// <summary>
        /// 攻击者自身卡帧停顿时间 (定点数秒，如 0.08s)
        /// </summary>
        public FP freeze;
        /// <summary>
        /// 可命中同一目标的最大次数
        /// </summary>
        public int canHitSameTarget;

        /// <summary>
        /// 多段命中同一目标的冷却间隔时间 (定点数秒)
        /// </summary>
        public FP hitSameTargetDelay;
        /// <summary>
        /// 命中时自身动作变换
        /// </summary>
        public ActionChangeInfo selfActionChange;

        /// <summary>
        /// 命中时对手动作变换
        /// </summary>
        public ActionChangeInfo targetActionChange;

        /// <summary>
        /// 命中时临时激活的 TempBeCancelledTag ID
        /// </summary>
        public string[] tempBeCancelledTagTurnOn;
    }
}
