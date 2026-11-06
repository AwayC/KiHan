using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 技能配置 (运行时，时间均已换算为逻辑帧)
    ///
    /// 一个技能对应一个按键与一个入口动作。多段技能不在这里配置：由动作指令 skillStage 把按键临时换成下一段动作
    /// (接段时限、超时返还 CD、该段图标都写在指令里)，与原版 1068 / 1092 / 1183 一致。CD 规则：
    /// - 释放入口动作或换上的下一段动作时立即开始计算完整 CD
    /// - skillStage 换段期间 CD 清零；超时未接：恢复按键并开始 CD，返还 cdReturn
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
        /// </summary>
        public string icon;

        public int cooldownTicks;

        /// <summary>
        /// 释放入口动作时消耗的查克拉 (颗)
        /// </summary>
        public int chakraCost;

        /// <summary>
        /// 只能在受击状态 (僵直 / 击飞 / 被抓 / 倒地) 下释放 (替身术)
        /// </summary>
        public bool requireHurt;

        /// <summary>
        /// 入口动作：由输入发起，受技能准入裁决
        /// </summary>
        public string actionId;

        /// <summary>
        /// 释放 (进入入口动作) 时给自己加的 Buff id，为空不加 (秘卷)
        /// </summary>
        public string buff;

        public SkillInfo Clone()
        {
            return (SkillInfo)MemberwiseClone();
        }
    }

    /// <summary>
    /// skills.json 顶层容器 DTO
    /// </summary>
    [Serializable]
    public class SkillDataContainer
    {
        [Tooltip("查克拉上限 (颗)，<= 0 时取默认 4")]
        public int chakraMax;

        [Tooltip("开局时的查克拉 (颗)，超过上限按上限计；默认 0")]
        public int chakraStart;

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

        [Tooltip("入口动作 id (多段技能的后续段由动作指令 skillStage 换上)")]
        public string actionId;

        [Tooltip("释放时给自己加的 Buff id (buffs.json)，可不填")]
        public string buff;

        public SkillInfo ToRuntime()
        {
            return new SkillInfo
            {
                id = id ?? string.Empty,
                button = ActionData.ParseEnum(button, CommandKey.None),
                icon = icon ?? string.Empty,
                cooldownTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(cooldown)),
                chakraCost = chakraCost,
                requireHurt = requireHurt,
                actionId = actionId ?? string.Empty,
                buff = buff ?? string.Empty
            };
        }
    }
}
