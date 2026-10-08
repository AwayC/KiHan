using System;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 运行时受击/外力强制位移状态跟踪器 (纯定点数确定性运算)
    /// </summary>
    public class ForceMove
    {
        public MoveInfo Data;
        public FP TimeElapsed = FP.Zero;
        public FP WasElapsed = FP.Zero;
        public Func<ForceMove, FPVector3> MoveTween = _ => FPVector3.zero;

        public static ForceMove FromData(MoveInfo data) => new ForceMove
        {
            Data = data,
            TimeElapsed = FP.Zero,
            WasElapsed = FP.Zero,
            MoveTween = ForceMoveMethod.Methods.ContainsKey(data.tweenMethod)
                ? ForceMoveMethod.Methods[data.tweenMethod]
                : _ => FPVector3.zero
        };

        public static ForceMove NoForceMove => new ForceMove
        {
            Data = new MoveInfo(),
            WasElapsed = FP.MaxValue,
            TimeElapsed = FP.MaxValue,
            MoveTween = _ => FPVector3.zero
        };

        /// <summary>
        /// 推进一个逻辑帧的位移时钟 (默认以 GameConfig.LOGIC_TICK_TIME_FP 推进)
        /// </summary>
        public void StepTick() => StepTick(GameConfig.LOGIC_TICK_TIME_FP);

        public void StepTick(FP deltaFP)
        {
            WasElapsed = TimeElapsed;
            TimeElapsed += deltaFP;
        }
    }
}
