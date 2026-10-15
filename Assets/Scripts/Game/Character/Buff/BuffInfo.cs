using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// Buff 配置 (运行时)：只有持续时间与循环表现特效，不限制层数，不修改属性
    /// 添加时表现层在角色身上播放循环特效 (可挂到锚点)，到期移除时停止该特效
    /// </summary>
    public class BuffInfo
    {
        public string id;
        public int durationTicks;

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

        [Tooltip("持续时间 (字符串秒数)")]
        public string duration;

        public string effect;
        public string anchor;
        public Vector2Int offset;

        public BuffInfo ToRuntime()
        {
            return new BuffInfo
            {
                id = id ?? string.Empty,
                durationTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(duration)),
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
    }
}
