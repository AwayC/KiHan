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
        /// 地面僵直时长 (定点数秒)：地面受击且未击飞时进入僵直，期间不能主动行动、循环播放受击动作
        /// 为 0 且未配置 hurtAction 时不施加僵直、不切换受击动作
        /// </summary>
        public FP hitStun;

        /// <summary>
        /// 地面僵直时的平推 (攻击者本地方向，按秒计时，与动作帧无关)；击飞、空中受击、倒地时不生效
        /// </summary>
        public MoveInfo push;

        /// <summary>
        /// 地面击飞 (含挑起倒地目标)：配置后命中地面目标即击飞
        /// </summary>
        public LaunchInfo launch;

        /// <summary>
        /// 命中浮空目标时的速度 (所有攻击都生效)；未配置时使用 GameConfig.DefaultAirLaunch
        /// </summary>
        public LaunchInfo air;

        /// <summary>
        /// 破霸等级：>= 受击者当前霸体等级时完整受击，否则只结算伤害
        /// </summary>
        public ArmorLevel armorBreak;

        /// <summary>
        /// 能否命中倒地 (Lie 姿态) 的目标 (扫地)
        /// </summary>
        public bool canHitLying;

        /// <summary>
        /// 抓取配置
        /// </summary>
        public GrabInfo grab;

        /// <summary>
        /// 吸附配置
        /// </summary>
        public SuctionInfo suction;

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
        /// 地面受击动作 id (僵直 / 击飞 / 被抓)，留空使用 HurtActionIds 的默认动作；空中受击固定为 hurt_inair
        /// </summary>
        public string hurtAction;

        /// <summary>
        /// 命中时临时激活的 TempBeCancelledTag ID
        /// </summary>
        public string[] tempBeCancelledTagTurnOn;
    }

    /// <summary>
    /// 击飞速度：z 竖直初速度，x / y 水平惯性 (单位/秒，x 为攻击者面朝方向前方)，覆盖目标当前速度
    /// 水平惯性在击飞流程中 (含触地弹起) 保持，弹不起倒地时清除
    /// </summary>
    [Serializable]
    public struct LaunchInfo
    {
        public bool enabled;
        public FP z;
        public FP x;
        public FP y;
    }
}
