using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// Buff 配置 (运行时)：持续时间、属性加成、叠层上限、按键倒计时显示与循环表现特效
    /// 也用作计时标记 (对应原版用 buff 当标记 + "是否有该 buff" 条件)：动作指令 addBuff / removeBuff 增删，条件 buff 读取剩余帧数
    /// 添加时表现层在角色身上播放循环特效 (可挂到锚点)，移除时停止该特效
    /// </summary>
    public class BuffInfo
    {
        public string id;
        public int durationTicks;

        /// <summary>
        /// 伤害加成 (每层累加，0.2 = 造成的伤害 +20%)
        /// </summary>
        public FP damageRate;

        /// <summary>
        /// 暴击率加成 (每层累加，0.15 = 暴击率 +15%)
        /// </summary>
        public FP critRate;

        /// <summary>
        /// 叠层上限 (0 = 不限)：已满时再添加只刷新最早一层的剩余时间
        /// </summary>
        public int maxStack;

        /// <summary>
        /// 在该按键的圆形进度条上显示剩余时间 (None = 不显示)
        /// </summary>
        public CommandKey button;

        /// <summary>
        /// 循环播放的特效资源
        /// </summary>
        public string effect;

        /// <summary>
        /// 特效跟随的帧库锚点 (为空时以脚底为原点)
        /// </summary>
        public string anchor;

        /// <summary>
        /// 相对锚点的整数像素偏移 (x 前方，y 高度)
        /// </summary>
        public Vector2Int offset;
    }

    /// <summary>
    /// buffs.json 顶层容器 DTO
    /// </summary>
    [Serializable]
    public class BuffDataContainer
    {
        public BuffData[] data;
    }

    [Serializable]
    public class BuffData
    {
        public string id;

        [Tooltip("持续时间 (字符串秒数，必须大于 0)")]
        public string duration;

        [Tooltip("伤害加成 (字符串，每层累加，\"0.2\" = 伤害 +20%)")]
        public string damageRate;

        [Tooltip("暴击率加成 (字符串，每层累加，\"0.15\" = 暴击率 +15%)")]
        public string critRate;

        [Tooltip("叠层上限 (0 = 不限)：已满时再添加只刷新最早一层的剩余时间")]
        public int maxStack;

        [Tooltip("在该按键的圆形进度条上显示剩余时间 (可不填)：Attack / Skill1 / Skill2 / Ultimate / Substitution / Secret / Summon")]
        public string button;

        public string effect;
        public string anchor;
        public Vector2Int offset;

        /// <summary>
        /// 按键名 -> 可显示倒计时的按键 (Attack ~ Summon，忽略大小写)
        /// </summary>
        public static bool TryParseButton(string name, out CommandKey key)
        {
            key = CommandKey.None;
            return !string.IsNullOrEmpty(name) && Enum.TryParse(name.Trim(), true, out key) && key >= CommandKey.Attack && key <= CommandKey.Summon;
        }

        public BuffInfo ToRuntime()
        {
            CommandKey key = CommandKey.None;
            if (!string.IsNullOrEmpty(button) && !TryParseButton(button, out key))
            {
                Debug.LogError($"[BuffData] Buff {id} 的按键无法识别: \"{button}\"，可选值: Attack / Skill1 / Skill2 / Ultimate / Substitution / Secret / Summon");
            }
            return new BuffInfo
            {
                id = id ?? string.Empty,
                durationTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(duration)),
                damageRate = ActionData.ParseFP(damageRate),
                critRate = ActionData.ParseFP(critRate),
                maxStack = maxStack,
                button = key,
                effect = effect ?? string.Empty,
                anchor = anchor ?? string.Empty,
                offset = offset
            };
        }
    }

    /// <summary>
    /// 角色身上的一层 Buff
    /// </summary>
    public struct BuffInstance
    {
        public BuffInfo Info;

        /// <summary>
        /// 角色内唯一的层编号 (表现层据此停止对应特效)
        /// </summary>
        public int Serial;

        public int RemainingTicks;

        /// <summary>
        /// 本层这次计时的总帧数 (按键倒计时比例用)
        /// </summary>
        public int TotalTicks;
    }
}
