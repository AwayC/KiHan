using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 通用受击动作 id (所有角色共用同一套受击流程，动作 id 固定，必须全部配置)
    ///
    /// 受击状态与姿态全部由这些固定 id 隐性决定，不再单独配置姿态：
    /// - 僵直：未指定 targetActionChange 时进入 hurt_1，僵直期间循环播放，解除后走 autoNext
    /// - 击飞：未指定 launchActionChange 时进入 hurt_toair，经 autoNext 链 (hurt_inair -> hurt_fall) 下落，落地强制进入 hurt_land
    /// - 抓取：未指定 targetActionChange 时进入 hurt_2，被抓期间循环播放
    /// - 倒地：当前动作为 lie 时视为倒地，只有 canHitLying 的攻击能命中
    /// - hurt_land -> lie -> standup -> idle 由各自的 autoNextActionId 串联 (起身无敌由 standup 的 armorPhase 配置)
    /// </summary>
    public static class HurtActionIds
    {
        public const string Light = "hurt_1";
        public const string Heavy = "hurt_2";
        public const string ToAir = "hurt_toair";
        public const string InAir = "hurt_inair";
        public const string Fall = "hurt_fall";
        public const string Land = "hurt_land";
        public const string Lie = "lie";
        public const string Standup = "standup";

        /// <summary>
        /// 自动切换受击动作的优先级 (需高于角色自身的主动打断与 autoNext)
        /// </summary>
        public const int Priority = 100;

        public static readonly string[] Required = { Light, Heavy, ToAir, InAir, Fall, Land, Lie, Standup };

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

        public static ActionChangeInfo ChangeTo(string id) => new ActionChangeInfo
        {
            changeType = ActionChangeType.ChangeToActionId,
            param = id,
            priority = Priority
        };
    }
}
