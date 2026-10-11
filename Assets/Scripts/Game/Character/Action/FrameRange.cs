using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动画帧闭区间 [min, max] (30fps 素材帧，每个逻辑帧推进 RENDER_LOGIC_RATIO 帧)
    /// </summary>
    [Serializable]
    public struct FrameRange
    {
        public int min;
        public int max;

        public FrameRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
