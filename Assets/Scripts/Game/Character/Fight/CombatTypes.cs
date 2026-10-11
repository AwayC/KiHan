using System;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 霸体等级 (数值越大越难被打断)
    /// 裁决规则：攻击的破霸等级 (AttackInfo.armorBreak) >= 受击者当前霸体等级时完整受击，否则只结算伤害
    /// 无敌 (Invincible) 直接不产生命中
    /// 攻击种类 (普通/技能/抓取/毒伤) 与霸体的细分矩阵后续再做
    /// </summary>
    public enum ArmorLevel
    {
        None = 0,       // 无霸体
        Skill = 1,      // 技能体
        Super = 2,      // 霸体
        KingKong = 3,   // 金刚体
        Invincible = 4, // 无敌
    }

    /// <summary>
    /// 一次命中对受击者产生的反应 (供表现层区分火花/跳字等)
    /// </summary>
    public enum HitReaction
    {
        /// <summary>被霸体抵挡，只结算伤害</summary>
        ArmorBlocked,
        /// <summary>地面僵直</summary>
        Stun,
        /// <summary>击飞 / 空中受击</summary>
        Launch,
        /// <summary>倒地中被扫地命中但未被挑起，只结算伤害</summary>
        Lying,
        /// <summary>被抓取</summary>
        Grab,
    }

    /// <summary>
    /// 霸体开启区间
    /// </summary>
    [Serializable]
    public struct ArmorPhaseInfo
    {
        /// <summary>
        /// 生效的动画帧区间 [min, max] (30fps 素材帧)
        /// </summary>
        public FrameRange range;

        public ArmorLevel level;
    }

    /// <summary>
    /// 抓取配置 (攻击命中后把目标锁定在攻击者帧锚点上)
    /// 锚点即抓取：攻击者每个逻辑帧最后处理的动画帧上存在该锚点则维持抓取并跟随锚点 (动作切换后同样生效)，
    /// 锚点消失 (主动松手 / 动作播完 / 被打断进入受击动作) 即释放；目标被其他命中改变受击状态 (如投掷命中) 时断开
    /// </summary>
    [Serializable]
    public struct GrabInfo
    {
        public const string DefaultAnchor = "grab";

        public bool enabled;

        /// <summary>
        /// 抓取锚点名称 (帧库 EffectAnchors 中的锚点，整数像素，相对攻击者脚底；x 随朝向镜像, y 为高度, 纵深与攻击者相同)
        /// </summary>
        public string anchor;

        /// <summary>
        /// 锚点消失而释放 (非投掷命中) 时目标的动作变化；目标在空中时转为击飞状态下落
        /// </summary>
        public ActionChangeInfo releaseActionChange;
    }

    /// <summary>
    /// 吸附配置 (命中后在一段时间内把目标向攻击者核心点水平拉扯)
    /// 只作用于水平面 (x, y)，竖直方向仍只走速度模型
    /// </summary>
    [Serializable]
    public struct SuctionInfo
    {
        /// <summary>
        /// 吸附核心点：相对攻击者脚底的本地偏移 (x 面朝方向前方, y 纵深)，随攻击者移动
        /// </summary>
        public FPVector3 center;

        /// <summary>
        /// 拉扯速度 (单位/秒)，每帧最多移动到核心点，不会越过
        /// </summary>
        public FP speed;

        /// <summary>
        /// 持续时间 (定点数秒，与动作帧无关)
        /// </summary>
        public FP inSec;

        public bool Enabled => speed > FP.Zero && inSec > FP.Zero;
    }
}
