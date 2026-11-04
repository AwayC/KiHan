using KiHan.Math;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 训练场伤害统计 (对敌人造成的伤害)：总伤害、命中次数、首次到最近一次命中的时长与秒伤。
    /// 超过 ResetSeconds 没有新的伤害时全部清零 (下一次命中重新开始计时)。时间按逻辑帧计，与帧率无关
    /// </summary>
    public sealed class DamageStats
    {
        public const int ResetSeconds = 6;
        private const int ResetTicks = ResetSeconds * GameConfig.LOGIC_FPS;

        /// <summary>
        /// 数据变化 (命中或清零) 时递增，界面据此只在变化时刷新
        /// </summary>
        public int Version { get; private set; }

        public FP TotalDamage { get; private set; }
        public int HitCount { get; private set; }

        private uint _firstFrame;
        private uint _lastFrame;

        /// <summary>
        /// 首次命中到最近一次命中的时长 (秒)
        /// </summary>
        public FP Seconds => HitCount == 0 ? FP.Zero : FP.FromInt((int)(_lastFrame - _firstFrame)) * GameConfig.LOGIC_TICK_TIME_FP;

        /// <summary>
        /// 秒伤：总伤害 / 时长 (时长不足 1 秒按 1 秒计，避免第一下命中时数值失真)
        /// </summary>
        public FP DamagePerSecond => HitCount == 0 ? FP.Zero : TotalDamage / FPMath.Max(Seconds, FP.One);

        public void Record(uint frameId, FP damage)
        {
            if (HitCount == 0) _firstFrame = frameId;
            _lastFrame = frameId;
            TotalDamage += damage;
            HitCount++;
            Version++;
        }

        /// <summary>
        /// 每个逻辑帧调用：距最近一次命中满 ResetSeconds 秒则清零
        /// </summary>
        public void Tick(uint frameId)
        {
            if (HitCount > 0 && frameId - _lastFrame >= ResetTicks) Clear();
        }

        public void Clear()
        {
            TotalDamage = FP.Zero;
            HitCount = 0;
            _firstFrame = 0;
            _lastFrame = 0;
            Version++;
        }
    }
}
