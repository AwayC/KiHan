using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 强制外力位移缓动算法字典 (基于定点数)
    /// </summary>
    public static class ForceMoveMethod
    {
        public static Dictionary<string, Func<ForceMove, FPVector3>> Methods =
            new Dictionary<string, Func<ForceMove, FPVector3>>
            {
                {
                    "Slowly",
                    (forceMove) =>
                    {
                        FP totalSec = forceMove.Data.inSec;
                        if (totalSec <= FP.Zero) return FPVector3.zero;

                        FP wasPec = FPMath.Clamp01(forceMove.WasElapsed / totalSec);
                        FP curPec = FPMath.Clamp01(forceMove.TimeElapsed / totalSec);

                        FPVector3 was = forceMove.Data.moveDistance * wasPec;
                        FPVector3 cur = forceMove.Data.moveDistance * curPec;
                        return cur - was;
                    }
                },
                {
                    "Linear",
                    (forceMove) =>
                    {
                        FP totalSec = forceMove.Data.inSec;
                        if (totalSec <= FP.Zero) return FPVector3.zero;

                        FP wasPec = FPMath.Clamp01(forceMove.WasElapsed / totalSec);
                        FP curPec = FPMath.Clamp01(forceMove.TimeElapsed / totalSec);

                        FPVector3 was = forceMove.Data.moveDistance * wasPec;
                        FPVector3 cur = forceMove.Data.moveDistance * curPec;
                        return cur - was;
                    }
                }
            };
    }
}
