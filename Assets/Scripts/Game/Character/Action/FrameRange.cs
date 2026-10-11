using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动画帧闭区间 [x, y] (30fps 素材帧，x 起始帧，y 结束帧；每个逻辑帧推进 RENDER_LOGIC_RATIO 帧)
    /// 配置写法：{ "x": 2, "y": 8 }
    /// </summary>
    [Serializable]
    public struct FrameRange
    {
        public int x;
        public int y;

        public FrameRange(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
