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

        /// <summary>
        /// 切换动作后产生的卡帧/受击硬直停顿时间 (定点数秒，如 0.2s)
        /// </summary>
        public FP FreezingAfterChangeAction;

        public PreorderActionInfo(string actionId, int priority = 0,
            int fromFrame = 0, FP freezingAfterChangeAction = default)
        {
            ActionId = actionId;
            Priority = priority;
            FromFrame = fromFrame;
            FreezingAfterChangeAction = freezingAfterChangeAction;
        }

        public PreorderActionInfo(string actionId, int priority,
            int fromFrame, string freezingAfterChangeActionSec)
        {
            ActionId = actionId;
            Priority = priority;
            FromFrame = fromFrame;
            FreezingAfterChangeAction = FP.Parse(freezingAfterChangeActionSec);
        }
    }
}
