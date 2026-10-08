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
        /// 允许打断的帧区间 (x: 起始帧, y: 结束帧)
        /// </summary>
        public Vector2 range;

        /// <summary>
        /// 可以 Cancel 的 CancelTag
        /// </summary>
        public string[] cancelTag;

        /// <summary>
        /// 当从这里被 Cancel，动作会增加多少优先级
        /// </summary>
        public int priority;

        /// <summary>
        /// 根据 TempBeCancelledTag 动态生成 BeCancelledTag
        /// </summary>
        public static BeCancelledTag FromTemp(TempBeCancelledTag tempTag, int currentFrame) => new BeCancelledTag
        {
            range = new Vector2(currentFrame, currentFrame + tempTag.startFrames),
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
