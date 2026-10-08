using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作 RootMotion 动态位移算法字典 (纯定点数确定性运算)
    /// </summary>
    public static class RootMotionMethod
    {
        public static Dictionary<string, Func<FP, string[], FPVector3>> Methods =
            new Dictionary<string, Func<FP, string[], FPVector3>>
            {
                {
                    "GoStraight",
                    (pec, param) =>
                    {
                        FP totalDis = param.Length > 0 ? FP.FromFloat(float.Parse(param[0])) : FP.Zero;
                        FP startPec = param.Length > 1 ? FP.FromFloat(float.Parse(param[1])) : FP.Zero;
                        FP endPec = param.Length > 2 ? FP.FromFloat(float.Parse(param[2])) : FP.One;
                        return pec <= startPec ? FPVector3.zero :
                            pec >= endPec ? new FPVector3(totalDis, FP.Zero, FP.Zero) : new FPVector3(pec * totalDis, FP.Zero, FP.Zero);
                    }
                }
            };
    }
}
