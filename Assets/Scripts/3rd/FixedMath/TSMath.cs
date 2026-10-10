using System;
using System.Runtime.CompilerServices;

namespace TrueSync
{
    /// <summary>
    /// 定点数数学函数库 (完全确定性，跨平台 0 浮点依赖)
    /// </summary>
    public static class TSMath
    {
        public static readonly FP Pi = FP.Pi;
        public static readonly FP TwoPi = FP.TwoPi;
        public static readonly FP HalfPi = FP.HalfPi;
        public static readonly FP Deg2Rad = FP.Deg2Rad;
        public static readonly FP Rad2Deg = FP.Rad2Deg;

        #region Min / Max / Clamp / Abs / Sign

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Min(FP val1, FP val2)
        {
            return val1 < val2 ? val1 : val2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Max(FP val1, FP val2)
        {
            return val1 > val2 ? val1 : val2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp(FP value, FP min, FP max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp01(FP value)
        {
            if (value < FP.Zero) return FP.Zero;
            if (value > FP.One) return FP.One;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP value)
        {
            return FP.Abs(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP value)
        {
            return FP.Sign(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Lerp(FP from, FP to, FP t)
        {
            t = Clamp01(t);
            return from + (to - from) * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP MoveTowards(FP current, FP target, FP maxDelta)
        {
            if (Abs(target - current) <= maxDelta) return target;
            return current + FP.Sign(target - current) * maxDelta;
        }

        #endregion

        #region Sqrt

        /// <summary>
        /// 牛顿-拉夫逊迭代法计算 Sqrt(value) 的定点数表示，7次迭代收敛至 0 误差
        /// </summary>
        public static FP Sqrt(FP value)
        {
            long raw = value._serializedValue;
            if (raw <= 0) return FP.Zero;

            int leadingZeros = CountLeadingZeros((ulong)raw);
            int bitLength = 64 - leadingZeros;
            int shift = (bitLength + FP.FRACTIONAL_PLACES) >> 1;
            FP guess = FP.FromRaw(1L << shift);

            for (int i = 0; i < 7; i++)
            {
                guess = FP.FromRaw((guess._serializedValue + (value / guess)._serializedValue) >> 1);
            }
            return guess;
        }

        private static int CountLeadingZeros(ulong x)
        {
            if (x == 0) return 64;
            int n = 0;
            if ((x & 0xFFFFFFFF00000000UL) == 0) { n += 32; x <<= 32; }
            if ((x & 0xFFFF000000000000UL) == 0) { n += 16; x <<= 16; }
            if ((x & 0xFF00000000000000UL) == 0) { n += 8;  x <<= 8;  }
            if ((x & 0xF000000000000000UL) == 0) { n += 4;  x <<= 4;  }
            if ((x & 0xC000000000000000UL) == 0) { n += 2;  x <<= 2;  }
            if ((x & 0x8000000000000000UL) == 0) { n += 1; }
            return n;
        }

        #endregion

        #region 三角函数 (Sin / Cos / Tan / Atan2)

        /// <summary>
        /// 查表法计算正弦值 (基于 1024 槽位 Lut 与线性插值，0 浮点误差)
        /// </summary>
        public static FP Sin(FP rad)
        {
            long rawRad = rad._serializedValue;
            long twoPi = FPLut.TWO_PI_RAW;

            // 归一化到 [0, 2*PI)
            rawRad = rawRad % twoPi;
            if (rawRad < 0) rawRad += twoPi;

            // 计算在 1024 槽位中的浮点索引: index = (rawRad * 1024) / twoPi
            // (rawRad * 1024) 相当于 rawRad << 10
            ulong scaled = (ulong)rawRad << 10;
            ulong idx = scaled / (ulong)twoPi;
            ulong rem = scaled % (ulong)twoPi; // 余数用于插值

            int index = (int)idx;
            if (index >= FPLut.LUT_SIZE) index = FPLut.LUT_SIZE - 1;

            long v0 = FPLut.SinLut[index];
            long v1 = FPLut.SinLut[index + 1];

            // 线性插值: v0 + (v1 - v0) * (rem / twoPi)
            long diff = v1 - v0;
            long interp = (long)(((ulong)Math.Abs(diff) * rem) / (ulong)twoPi);
            long resultRaw = diff >= 0 ? v0 + interp : v0 - interp;

            return FP.FromRaw(resultRaw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Cos(FP rad)
        {
            return Sin(rad + HalfPi);
        }

        public static FP Tan(FP rad)
        {
            FP cosVal = Cos(rad);
            if (cosVal == FP.Zero) return FP.MaxValue;
            return Sin(rad) / cosVal;
        }

        /// <summary>
        /// 确定性高精度 Atan2(y, x) 逼近算法
        /// </summary>
        public static FP Atan2(FP y, FP x)
        {
            if (x == FP.Zero)
            {
                if (y > FP.Zero) return HalfPi;
                if (y < FP.Zero) return -HalfPi;
                return FP.Zero;
            }

            FP absX = Abs(x);
            FP absY = Abs(y);

            FP res;
            if (absX >= absY)
            {
                FP t = y / x;
                res = AtanPolynomial(t);
                if (x < FP.Zero)
                {
                    res += (y >= FP.Zero ? Pi : -Pi);
                }
            }
            else
            {
                FP t = x / y;
                res = (y > FP.Zero ? HalfPi : -HalfPi) - AtanPolynomial(t);
            }
            return res;
        }

        // 多项式展开系数 (Q32.32 预计算纯整数常量，彻底消除运行时浮点转换开销与跨平台 FPU 尾数偏差)
        private static readonly FP AtanC1 = FP.FromRaw(4294391770L); // 0.999866
        private static readonly FP AtanC2 = FP.FromRaw(1418625550L); // 0.3302995
        private static readonly FP AtanC3 = FP.FromRaw(773699703L);  // 0.180141
        private static readonly FP AtanC4 = FP.FromRaw(365643450L);  // 0.085133
        private static readonly FP AtanC5 = FP.FromRaw(89486073L);   // 0.0208351

        /// <summary>
        /// 针对 |z| <= 1 的高精度奇多项式展开: z * (0.999866 - 0.3302995*z^2 + 0.180141*z^4 - ...)
        /// </summary>
        private static FP AtanPolynomial(FP z)
        {
            FP z2 = z * z;
            FP poly = AtanC1 - z2 * (AtanC2 - z2 * (AtanC3 - z2 * (AtanC4 - z2 * AtanC5)));
            return z * poly;
        }

        #endregion
    }
}
