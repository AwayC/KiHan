namespace KiHan.Logic
{
    public static class GameConfig
    {
        // 逻辑层：15 FPS (负责判定、位移)
        //public const int LOGIC_FPS = 15;
        public const float LOGIC_TICK_TIME = 0.066f; // 0.0666...f

        // 渲染/动画层：30 FPS (负责动画播放、平滑插值)
        //public const int RENDER_FPS = 30;
        public const float RENDER_TICK_TIME = 0.033f; // 0.0333...f

        // 逻辑帧与渲染帧的比例 (30 / 15 = 2)
        public const int RENDER_LOGIC_RATIO = (int)(LOGIC_TICK_TIME / RENDER_TICK_TIME);
    }
}
