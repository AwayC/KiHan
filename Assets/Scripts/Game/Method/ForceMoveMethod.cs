using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 位移缓动曲线字典 (基于定点数)，强制外力位移 (ForceMove) 与 RootMotion 分段共用
    /// 输入归一化时间进度 t ∈ [0, 1]，输出归一化累计位移 [0, 1]
    /// 委托在静态初始化时创建，运行期查表复用，0 GC
    /// </summary>
    public static class ForceMoveMethod
    {
        public static readonly Func<FP, FP> Linear = t => t;

        /// <summary>
        /// 减速位移 (QuadOut)：受击平推起步快、收尾慢
        /// </summary>
        public static readonly Func<FP, FP> Slowly = t =>
        {
            FP inv = FP.One - t;
            return FP.One - inv * inv;
        };

        public static readonly Dictionary<string, Func<FP, FP>> Methods =
            new Dictionary<string, Func<FP, FP>>
            {
                { "Linear", Linear },
                { "Slowly", Slowly }
            };

        /// <summary>
        /// 按名称取缓动曲线，未配置或找不到时退化为 Linear
        /// </summary>
        public static Func<FP, FP> Get(string name)
        {
            if (!string.IsNullOrEmpty(name) && Methods.TryGetValue(name, out Func<FP, FP> ease)) return ease;
            return Linear;
        }
    }
}
