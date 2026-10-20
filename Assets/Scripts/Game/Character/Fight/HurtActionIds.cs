using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 通用受击动作 id (所有角色共用同一套受击流程，动作 id 固定，必须全部配置)
    ///
    /// 受击流程由框架驱动 (CharacterObj)，受击动作只提供动画，autoNextActionId 不参与：
    /// - 僵直：未指定 hurtAction 时进入 hurt_1，僵直期间循环播放，时间到回到默认动作
    /// - 抓取：未指定 hurtAction 时进入 hurt_2，被抓期间循环播放
    /// - 击飞：地面被击飞默认 hurt_toair (可被 hurtAction 覆盖)，空中受击固定 hurt_inair；
    ///         hurt_toair -> hurt_inair -> hurt_fall (循环到落地)
    /// - 触地：弹起速度 = 落地速度 × 衰减系数，>= 最小弹起速度则弹起 (播 hurt_inair，无伤害，保持击飞)，否则倒地
    /// - 倒地：播放 lie，播完起身 (倒地时长 = lie 动画长度)；只有 canHitLying 的攻击能命中
    /// - 起身：播放 standup，期间无敌，播完回到默认动作并追加 standupInvincibleSec 秒无敌
    /// </summary>
    public static class HurtActionIds
    {
        public const string Light = "hurt_1";
        public const string Heavy = "hurt_2";
        public const string ToAir = "hurt_toair";
        public const string InAir = "hurt_inair";
        public const string Fall = "hurt_fall";
        public const string Lie = "lie";
        public const string Standup = "standup";

        /// <summary>
        /// 命中切换受击动作的优先级 (需高于角色自身的主动打断与 autoNext)
        /// </summary>
        public const int Priority = 100;

        /// <summary>
        /// 触地结果 (弹起 / 倒地) 的优先级：高于同帧的动画播完推进，低于命中
        /// </summary>
        public const int LandPriority = 95;

        /// <summary>
        /// 受击动作播完时的流程推进 (上升转空中、下落循环、倒地转起身、回到默认动作) 的优先级，低于触地与命中
        /// </summary>
        public const int FlowPriority = 90;

        public static readonly string[] Required = { Light, Heavy, ToAir, InAir, Fall, Lie, Standup };

        /// <summary>
        /// 返回动作库中缺失的受击动作 id，全部存在时返回空列表
        /// </summary>
        public static List<string> FindMissing(List<ActionInfo> actions)
        {
            List<string> missing = new List<string>();
            foreach (string id in Required)
            {
                bool found = false;
                if (actions != null)
                {
                    foreach (ActionInfo info in actions)
                    {
                        if (info.id == id)
                        {
                            found = true;
                            break;
                        }
                    }
                }
                if (!found) missing.Add(id);
            }
            return missing;
        }

        public static ActionChangeInfo ChangeTo(string id, int priority = Priority) => new ActionChangeInfo
        {
            changeType = ActionChangeType.ChangeToActionId,
            param = id,
            priority = priority
        };
    }
}
