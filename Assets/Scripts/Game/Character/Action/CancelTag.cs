using System;
using UnityEngine;

namespace KiHan.Logic
{
    [Serializable]
    public struct CancelTag
    {
        public string tag;

        /// <summary>
        /// 动作从多少帧开始播放，可以取消前摇
        /// </summary>
        public int startFormFrame;

        /// <summary>
        /// 优先级
        /// </summary>
        public int priority;
    }

    [Serializable]
    public struct BeCancelledTag
    {
        /// <summary>
        /// 允许打断的动画帧闭区间 (30fps 素材帧，x: 起始帧, y: 结束帧)：本逻辑帧处理到其中任意一帧后即可切换
        /// </summary>
        public Vector2Int range;

        /// <summary>
        /// 可以 Cancel 的 CancelTag
        /// </summary>
        public string[] cancelTag;

        /// <summary>
        /// 当从这里被 Cancel，动作会增加多少优先级
        /// </summary>
        public int priority;

        /// <summary>
        /// 根据 TempBeCancelledTag 动态生成 BeCancelledTag：从下一个要处理的动画帧 nextFrame 起，持续 startFrames 帧
        /// </summary>
        public static BeCancelledTag FromTemp(TempBeCancelledTag tempTag, int nextFrame) => new BeCancelledTag
        {
            range = new Vector2Int(nextFrame, nextFrame + tempTag.startFrames - 1),
            cancelTag = tempTag.cancelTag,
            priority = tempTag.priority
        };
    }

    [Serializable]
    public struct TempBeCancelledTag
    {
        /// <summary>
        /// 索引 ID
        /// </summary>
        public string id;

        /// <summary>
        /// 开启多少帧
        /// </summary>
        public int startFrames;

        /// <summary>
        /// 可以 Cancel 的 CancelTag
        /// </summary>
        public string[] cancelTag;

        /// <summary>
        /// 优先级
        /// </summary>
        public int priority;
    }
}
