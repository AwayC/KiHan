using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 范围区间 (支持帧数区间或百分比区间 [min, max])
    /// </summary>
    [Serializable]
    public struct PercentageRange
    {
        public float min;
        public float max;

        public PercentageRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
