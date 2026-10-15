using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 技能配置 (运行时，时间均已换算为逻辑帧)
    ///
    /// 多段技能：每一段由入口动作 (entry) 发起，同一段的后续动作 (落地、命中派生、收招) 列在 followActions 中，
    /// 期间都算作"正在释放该段"。CD 规则：
    /// - 释放完最后一段 (离开最后一段的全部动作) 后开始计算完整 CD
    /// - 某一段放完后在 stageDuration 内没有接下一段：开始计算 CD，并返还该段的 cdReturn
    /// </summary>
    public class SkillInfo
    {
        public string id;

        /// <summary>
        /// UI 显示所在的按键 (仅表现层使用，出招指令仍由入口动作的 commands 决定)
        /// </summary>
        public CommandKey button;

        /// <summary>
        /// 按键图标 (仅表现层使用)：Resources 路径；多图切片贴图用 "路径#子图名"
        /// 某一段配置了自己的图标时，轮到该段时显示该段图标
        /// </summary>
        public string icon;

        public int cooldownTicks;

        /// <summary>
        /// 释放第一段时消耗的查克拉 (颗)
        /// </summary>
        public int chakraCost;

        /// <summary>
        /// 只能在受击状态 (僵直 / 击飞 / 被抓 / 倒地) 下释放 (替身术)
        /// </summary>
        public bool requireHurt;

        public SkillStageInfo[] stages;

        /// <summary>
        /// 第 stage 段 (0 起) 应显示的图标地址：该段配置了图标用该段的，否则用技能图标
        /// </summary>
        public string IconOfStage(int stage)
        {
            if (stage >= 0 && stage < stages.Length && !string.IsNullOrEmpty(stages[stage].icon)) return stages[stage].icon;
            return icon;
        }
    }

    public struct SkillStageInfo
    {
        /// <summary>
        /// 入口动作：由输入发起，受技能准入裁决
        /// </summary>
        public string actionId;

        /// <summary>
        /// 同一段的后续动作 (由 autoNext / 落地 / 命中进入，不受准入裁决)
        /// </summary>
        public string[] followActions;

        /// <summary>
        /// 本段放完后接下一段的时限 (逻辑帧)
        /// </summary>
        public int windowTicks;

        /// <summary>
        /// 本段之后的段没有使用时返还的 CD (逻辑帧)
        /// </summary>
        public int cdReturnTicks;

        /// <summary>
        /// 轮到本段时的按键图标 (仅表现层使用)，为空沿用技能图标
        /// </summary>
        public string icon;
    }

    /// <summary>
    /// skills.json 顶层容器 DTO
    /// </summary>
    [Serializable]
    public class SkillDataContainer
    {
        [Tooltip("查克拉上限 (颗)，<= 0 时取默认 4")]
        public int chakraMax;

        public SkillData[] data;
    }

    /// <summary>
    /// 技能配置 DTO (String-First：时间为字符串秒数)
    /// </summary>
    [Serializable]
    public class SkillData
    {
        public string id;

        [Tooltip("UI 按键：Skill1 / Skill2 / Ultimate / Substitution ...")]
        public string button;

        [Tooltip("按键图标：Resources 路径，多图切片贴图用 \"路径#子图名\"")]
        public string icon;

        [Tooltip("CD (字符串秒数)")]
        public string cooldown;

        public int chakraCost;
        public bool requireHurt;
        public SkillStageData[] stages;

        public SkillInfo ToRuntime()
        {
            var info = new SkillInfo
            {
                id = id ?? string.Empty,
                button = ActionData.ParseEnum(button, CommandKey.None),
                icon = icon ?? string.Empty,
                cooldownTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(cooldown)),
                chakraCost = chakraCost,
                requireHurt = requireHurt,
                stages = new SkillStageInfo[stages != null ? stages.Length : 0]
            };
            for (int i = 0; i < info.stages.Length; i++)
            {
                info.stages[i] = stages[i].ToRuntime();
            }
            return info;
        }
    }

    [Serializable]
    public struct SkillStageData
    {
        public string actionId;
        public string[] followActions;

        [Tooltip("本段放完后接下一段的时限 (字符串秒数)")]
        public string stageDuration;

        [Tooltip("之后的段没有使用时返还的 CD (字符串秒数)")]
        public string cdReturn;

        [Tooltip("轮到本段时的按键图标，留空沿用技能图标")]
        public string icon;

        public SkillStageInfo ToRuntime()
        {
            return new SkillStageInfo
            {
                actionId = actionId ?? string.Empty,
                followActions = followActions ?? Array.Empty<string>(),
                windowTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(stageDuration)),
                cdReturnTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(cdReturn)),
                icon = icon ?? string.Empty
            };
        }
    }
}
