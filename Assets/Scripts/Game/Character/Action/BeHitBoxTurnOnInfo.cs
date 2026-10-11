using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 受击判定盒开启的时段信息
    /// </summary>
    [Serializable]
    public struct BeHitBoxTurnOnInfo
    {
        [Tooltip("开启的帧数/百分比区间")]
        public FrameRange[] frameRanges;

        [Tooltip("开启的受击盒 Tag")]
        public string[] tag;

        [Tooltip("优先级")]
        public int priority;

        [Tooltip("受击时临时开启的被取消标签")]
        public string[] tempBeCancelledTagTurnOn;

        [Tooltip("受击时攻击方动作变化")]
        public ActionChangeInfo attackerActionChange;

        [Tooltip("受击时自身动作变化")]
        public ActionChangeInfo selfActionChange;
    }
}
