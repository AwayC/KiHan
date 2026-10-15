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
        /// 目标僵直时长 (定点数秒，如 0.3s)：地面受击进入僵直状态，期间不能主动行动、循环播放受击动作
        /// 为 0 表示该攻击不施加僵直 (不改变目标当前的僵直)
        /// </summary>
        public FP hitStun;

        /// <summary>
        /// 击飞竖直初速度 (单位/秒)：大于 0 时目标进入击飞状态；目标已在空中时无论是否配置都进入击飞状态
        /// </summary>
        public FP launchVelocity;

        /// <summary>
        /// 命中空中目标时设置的竖直速度 (空中追击上挑)；为 0 时沿用 launchVelocity，两者都为 0 则保持目标当前竖直速度
        /// </summary>
        public FP airLaunchVelocity;

        /// <summary>
        /// 击飞时给目标的水平惯性速度 (单位/秒；x 为攻击者面朝方向前方, y 纵深)
        /// 每次击飞都会覆盖目标当前的水平惯性 (未配置即清零)；击飞流程中 (含触地弹起) 一直保持，落地倒地时清除
        /// </summary>
        public FP launchVelocityX;
        public FP launchVelocityY;

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
        /// 停帧时长 (定点数秒)：预留给全局停帧 (freeze)，当前未生效
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
        /// 命中时对手动作变换 (僵直状态使用，如 ChangeToActionId hurt_1)
        /// </summary>
        public ActionChangeInfo targetActionChange;

        /// <summary>
        /// 命中使对手进入击飞状态时的动作变换 (如 hurt_toair)；未配置 (Keep) 时沿用 targetActionChange
        /// </summary>
        public ActionChangeInfo launchActionChange;

        /// <summary>
        /// 命中时临时激活的 TempBeCancelledTag ID
        /// </summary>
        public string[] tempBeCancelledTagTurnOn;
    }
}
