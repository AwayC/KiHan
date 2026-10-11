using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 攻击判定盒开启的时段信息
    /// </summary>
    [Serializable]
    public struct AttackBoxTurnOnInfo
    {
        [Tooltip("开启的帧数/百分比区间")]
        public FrameRange[] frameRanges;

        [Tooltip("开启的攻击盒 Tag")]
        public string[] tag;

        [Tooltip("对应 ActionInfo.attacks 中的第几段伤害")]
        public int attackPhase;

        [Tooltip("优先级")]
        public int priority;
    }
}
