using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 命中分支：动画推进经过 frame 时，若角色的命中记录为真则切换到 actionId (对应原版 1015 → 1005 跳段)
    /// 命中记录跨动作保留，由动作的 clearHitRecord 清空、recordHitFrames 期间的命中写入；
    /// 因此"空中段命中、落地段再判断"这类跨段条件只需在后一段配置分支，不必拆出命中 / 未命中两套动作
    /// 配置写法：{ "frame": 13, "actionId": "skill2_2", "priority": 50 }
    /// </summary>
    [Serializable]
    public struct HitBranch
    {
        [Tooltip("检查的动画帧 (本逻辑帧推进经过该帧时检查一次)")]
        public int frame;

        [Tooltip("命中记录为真时切换到的动作 id")]
        public string actionId;

        [Tooltip("切换预约的优先级 (与当前动作 priority 相加)，低于受击流程 (90/100) 时被打断则不切换")]
        public int priority;
    }
}
