using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 通用受击动作 id (所有角色共用同一套受击流程，动作 id 固定；对应原版基础动画 get_hit / catch / Float / Float_air / lie_down / lie_got_up)
    ///
    /// 受击流程由框架驱动 (CharacterObj)，受击动作只提供动画，autoNextActionId 不参与：
    /// - 僵直：未指定 hurtAction 时从已配置的 hurt / hurt_1 / hurt_2 / hurt_3 中确定性随机选一个，僵直期间循环播放，时间到回到默认动作
    /// - 抓取：抓取攻击的 grab.pose 指定被抓姿势 (动作 + 切片步 + 是否定格)；未指定时用 hurtAction，再没有则进入 grabbed，被抓期间循环播放
    /// - 浮空：地面被击飞默认 float 从头播放 (可被 hurtAction 覆盖)；空中受击与触地弹起播 float，从其 airHitStep 切片步开始；
    ///         float 播完转 fall，fall 循环到触地
    /// - 触地：弹起速度 = 落地速度 × 衰减系数，>= 最小弹起速度则弹起 (无伤害，保持击飞)，否则倒地
    /// - 倒地：循环播放 lie，倒地时长固定 (CharacterObj.lieSec)；只有 canHitLying 的攻击能命中
    /// - 起身：播放 standup，期间无敌，播完回到默认动作并追加 standupInvincibleSec 秒无敌
    /// </summary>
    public static class HurtActionIds
    {
        public const string Stand = "hurt";
        public const string Grabbed = "grabbed";
        public const string Float = "float";
        public const string Fall = "fall";
        public const string Lie = "lie";
        public const string Standup = "standup";

        /// <summary>
        /// 地面僵直动作的全部变体 (hurt 必须配置，其余可选)：未指定 hurtAction 时在已配置的变体中确定性随机
        /// </summary>
        public static readonly string[] StandVariants = { Stand, "hurt_1", "hurt_2", "hurt_3" };

        /// <summary>
        /// 命中切换受击动作的优先级 (需高于角色自身的主动打断与 autoNext)
        /// </summary>
        public const int Priority = 100;

        /// <summary>
        /// 触地结果 (弹起 / 倒地) 的优先级：高于同帧的动画播完推进，低于命中
        /// </summary>
        public const int LandPriority = 95;

        /// <summary>
        /// 受击动作播完时的流程推进 (浮空转下落、下落循环、起身后回到默认动作) 的优先级，低于触地与命中
        /// </summary>
        public const int FlowPriority = 90;

        public static readonly string[] Required = { Stand, Grabbed, Float, Fall, Lie, Standup };

        /// <summary>
        /// 返回动作库中缺失的受击动作 id，全部存在时返回空列表
        /// </summary>
        public static List<string> FindMissing(List<ActionInfo> actions)
        {
            List<string> missing = new List<string>();
            foreach (string id in Required)
            {
                if (!Contains(actions, id)) missing.Add(id);
            }
            return missing;
        }

        /// <summary>
        /// 动作库中已配置的僵直变体 (按 StandVariants 顺序)，结果写入 result (调用方复用列表，0 GC)
        /// </summary>
        public static void CollectStandVariants(List<ActionInfo> actions, List<string> result)
        {
            result.Clear();
            foreach (string id in StandVariants)
            {
                if (Contains(actions, id)) result.Add(id);
            }
        }

        /// <summary>
        /// 确定性随机选择僵直变体：同一逻辑帧、同一对攻防双方在所有客户端得到同一结果
        /// </summary>
        public static string PickStand(List<string> variants, uint frameId, int targetId, int attackerId)
        {
            if (variants == null || variants.Count == 0) return Stand;
            if (variants.Count == 1) return variants[0];

            return variants[(int)(LogicHash.Mix(frameId, targetId, attackerId) % (uint)variants.Count)];
        }

        public static ActionChangeInfo ChangeTo(string id, int priority = Priority, int fromFrame = 0, bool hold = false) => new ActionChangeInfo
        {
            changeType = ActionChangeType.ChangeToActionId,
            param = id,
            priority = priority,
            fromFrame = fromFrame,
            hold = hold
        };

        private static bool Contains(List<ActionInfo> actions, string id)
        {
            if (actions == null) return false;
            foreach (ActionInfo info in actions)
            {
                if (info.id == id) return true;
            }
            return false;
        }
    }
}
