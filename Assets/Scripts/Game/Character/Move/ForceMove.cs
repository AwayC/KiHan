using System;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 运行时受击/外力强制位移状态 (纯定点数确定性运算)
    /// 值类型，作为角色字段原地复用：命中时 Start 覆盖，结束或打断时 Stop，0 GC
    /// 按时间 (秒) 配置，与动作帧无关；卡帧期间不调用 Step，计时随之暂停
    /// </summary>
    public struct ForceMove
    {
        public MoveInfo Data;

        /// <summary>
        /// 已流逝时间 (定点数秒)
        /// </summary>
        public FP TimeElapsed;

        private Func<FP, FP> _ease;

        /// <summary>
        /// 是否仍在施加外力
        /// </summary>
        public bool Active => _ease != null && TimeElapsed < Data.inSec;

        /// <summary>
        /// 开始一段新的外力位移 (覆盖当前外力)
        /// </summary>
        /// <param name="worldData">世界空间位移配置 (本地配置需先经 MoveInfo.ToWorld 转换)</param>
        public void Start(MoveInfo worldData)
        {
            Data = worldData;
            TimeElapsed = FP.Zero;
            _ease = worldData.inSec > FP.Zero ? ForceMoveMethod.Get(worldData.tweenMethod) : null;
        }

        public void Stop()
        {
            Data = default;
            TimeElapsed = FP.Zero;
            _ease = null;
        }

        /// <summary>
        /// 推进一个逻辑帧并返回本帧位移 = 总位移 × (ease(当前进度) - ease(上帧进度))
        /// </summary>
        public FPVector3 Step(FP deltaFP)
        {
            if (!Active) return FPVector3.zero;

            FP total = Data.inSec;
            FP wasPec = _ease(FPMath.Clamp01(TimeElapsed / total));
            TimeElapsed += deltaFP;
            FP curPec = _ease(FPMath.Clamp01(TimeElapsed / total));

            return Data.moveDistance * (curPec - wasPec);
        }
    }
}
