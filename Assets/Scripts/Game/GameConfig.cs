using TrueSync;

namespace KiHan.Logic
{
    public static class GameConfig
    {
        // ----------------- 逻辑层 (30 FPS 纯定点数确定性) -----------------
        public const int LOGIC_FPS = 30;

        /// <summary>
        /// 纯定点数单逻辑帧时长 (1 / 30 ≈ 0.0333333333 秒，Q32.32 精度，零累积漂移)
        /// </summary>
        public static readonly FP LOGIC_TICK_TIME_FP = FP.One / LOGIC_FPS;

        /// <summary>
        /// 表现层/平滑插值基准浮点时间 (保持兼容老版本与 View 层 Lerp 运算)
        /// 严格使用 1f / LOGIC_FPS，杜绝写死 0.033f 之类近似值导致的累积漂移
        /// </summary>
        public const float LOGIC_TICK_TIME = 1f / LOGIC_FPS;

        // ----------------- 渲染/动画层 (30 FPS 自由帧率插值) -----------------
        public const int RENDER_FPS = 30;
        public const float RENDER_TICK_TIME = 1f / RENDER_FPS;

        // 每个逻辑帧推进的动画素材帧数 (素材 30fps / 同步 30fps = 1)
        public const int RENDER_LOGIC_RATIO = RENDER_FPS / LOGIC_FPS;

        // ----------------- 判定盒像素单位换算 -----------------
        /// <summary>
        /// 判定盒/帧库像素 -> 逻辑单位 (100 像素 = 1 逻辑单位)
        /// </summary>
        public const int PIXELS_PER_UNIT = 100;

        public static FP PixelToUnit(int px) => FP.FromInt(px) / PIXELS_PER_UNIT;

        /// <summary>
        /// 定点数秒 -> 逻辑帧数 (向上取整，不足一帧按一帧计)
        /// </summary>
        public static int SecondsToTicks(FP seconds) => seconds > FP.Zero ? (int)FP.Ceiling(seconds * LOGIC_FPS) : 0;
    }
}
