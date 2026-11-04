using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 一次命中判定结果 (检测阶段产出，结算阶段消费，同帧所有命中先收集再统一结算)
    /// </summary>
    public struct HitResult
    {
        public CharacterObj Attacker;
        public CharacterObj Target;

        /// <summary>
        /// 命中的伤害段 (ActionInfo.attacks 下标)
        /// </summary>
        public int AttackPhase;

        /// <summary>
        /// 命中发生在攻击方动作的哪个动画帧 (命中记录 recordHitFrames 按此判断)
        /// </summary>
        public int Frame;

        public AttackInfo Attack;

        /// <summary>
        /// 目标被命中的受击阶段信息 (未配置 defensePhase 时为默认值)
        /// </summary>
        public BeHitBoxTurnOnInfo Defense;

        /// <summary>
        /// 攻击盒与受击盒重叠区域中心 (逻辑坐标)
        /// </summary>
        public FPVector3 HitPoint;

        /// <summary>
        /// 攻击盒阶段优先级 + 受击盒阶段优先级
        /// </summary>
        public int Priority;
    }

    /// <summary>
    /// 打击事件 (逻辑层 -> 表现层单向通知，纯数据：火花、跳字、震屏等由表现层据此生成)
    /// </summary>
    public struct HitEvent
    {
        public uint FrameId;
        public int AttackerId;

        /// <summary>
        /// 攻击者的根角色 (生成物命中时为其主人；玩家直接命中时等于 AttackerId)
        /// </summary>
        public int OwnerId;

        public int TargetId;
        public int AttackPhase;

        /// <summary>
        /// 实际伤害 (暴击时已乘暴击倍率)
        /// </summary>
        public FP Damage;

        /// <summary>
        /// 是否暴击
        /// </summary>
        public bool Critical;

        public FPVector3 HitPoint;

        /// <summary>
        /// 受击者的反应 (霸体抵挡 / 僵直 / 击飞 / 倒地 / 抓取)
        /// </summary>
        public HitReaction Reaction;
    }
}
