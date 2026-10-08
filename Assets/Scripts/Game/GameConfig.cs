using TrueSync;

namespace KiHan.Logic
{
    public static class GameConfig
    {
        // ----------------- 逻辑层 (15 FPS 纯定点数确定性) -----------------
        public const int LOGIC_FPS = 15;

        /// <summary>
        /// 纯定点数单逻辑帧时长 (1 / 15 ≈ 0.0666666667 秒，Q32.32 精度，零累积漂移)
        /// </summary>
        public static readonly FP LOGIC_TICK_TIME_FP = FP.One / LOGIC_FPS;

        /// <summary>
        /// 表现层/平滑插值基准浮点时间 (保持兼容老版本与 View 层 Lerp 运算)
        /// 严格使用 1f / 15f，杜绝 0.066f 导致的每秒 10ms 漂移
        /// </summary>
        public const float LOGIC_TICK_TIME = 1f / LOGIC_FPS;

        // ----------------- 渲染/动画层 (30 FPS 自由帧率插值) -----------------
        public const int RENDER_FPS = 30;
        public const float RENDER_TICK_TIME = 1f / RENDER_FPS;

        // 逻辑帧与渲染帧的比例 (30 / 15 = 2)
        public const int RENDER_LOGIC_RATIO = RENDER_FPS / LOGIC_FPS;
    }
}
