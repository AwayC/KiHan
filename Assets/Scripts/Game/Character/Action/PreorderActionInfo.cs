using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作切换预约信息 (基于定点数时间与优先级)
    /// </summary>
    public struct PreorderActionInfo
    {
        /// <summary>
        /// 目标动作 ID
        /// </summary>
        public string ActionId;

        /// <summary>
        /// 仲裁优先级 (数值越高越优先选中)
        /// </summary>
        public int Priority;

        /// <summary>
        /// 开始播放的逻辑帧索引
        /// </summary>
        public int FromFrame;

        public PreorderActionInfo(string actionId, int priority = 0, int fromFrame = 0)
        {
            ActionId = actionId;
            Priority = priority;
            FromFrame = fromFrame;
        }
    }
}
