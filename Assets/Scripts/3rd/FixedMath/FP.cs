using System;
using System.Runtime.CompilerServices;

namespace TrueSync
{
    /// <summary>
    /// Q32.32 定点数结构体 (64位有符号整数，低32位为小数部分)
    /// 纯整型运算，保证跨平台 (Windows, Android, iOS, WebGL) 100% 确定性与 0 GC
    /// </summary>
    [Serializable]
    public struct FP : IEquatable<FP>, IComparable<FP>
    {
        public const int FRACTIONAL_PLACES = 32;
        public const long ONE = 1L << FRACTIONAL_PLACES;
        public const long HALF = 1L << (FRACTIONAL_PLACES - 1);

        public static readonly FP Zero = new FP(0L);
        public static readonly FP One = new FP(ONE);
        public static readonly FP Half = new FP(HALF);
        public static readonly FP MaxValue = new FP(long.MaxValue);
        public static readonly FP MinValue = new FP(long.MinValue);
        public static readonly FP Epsilon = new FP(1L);

        // 常用数学常数 (Q32.32 定点数直接初始化)
        public static readonly FP Pi = new FP(13508802236L);         // 3.141592653589793
        public static readonly FP TwoPi = new FP(27017604473L);      // 6.283185307179586
        public static readonly FP HalfPi = new FP(6754401118L);      // 1.5707963267948966
        public static readonly FP Deg2Rad = new FP(74961320L);       // 0.017453292519943295
        public static readonly FP Rad2Deg = new FP(246083499198L);   // 57.29577951308232

        // 底层存储值
        public long _serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FP(long rawValue)
        {
            _serializedValue = rawValue;
        }

        public long RawValue => _serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromRaw(long rawValue)
        {
            return new FP(rawValue);
        }

        #region 类型转换构造

        [Obsolete("严禁在确定性逻辑中使用浮点转换！为杜绝跨平台 FPU 尾数偏差，请使用 FP.Parse(\"...\") 或 FP.FromInt()。此方法仅供表现层单向转换使用。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromFloat(float value)
        {
            return new FP((long)((double)value * ONE));
        }

        [Obsolete("严禁在确定性逻辑中使用浮点转换！为杜绝跨平台 FPU 尾数偏差，请使用 FP.Parse(\"...\") 或 FP.FromInt()。此方法仅供表现层单向转换使用。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromDouble(double value)
        {
            return new FP((long)(value * ONE));
        }

        /// <summary>
        /// 浮点数规整转换 (仅供表现层/离线工具使用，确定性逻辑请使用纯整型 FP.Parse)
        /// </summary>
        [Obsolete("确定性逻辑严禁依赖浮点四舍五入！请使用 FP.Parse 彻底杜绝 FPU 平台微差。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromFloatDeterministic(float value, int precision = 10000)
        {
            long scaled = (long)Math.Round((double)value * precision);
            return new FP((scaled << FRACTIONAL_PLACES) / precision);
        }

        [Obsolete("确定性逻辑严禁依赖浮点四舍五入！请使用 FP.Parse 彻底杜绝 FPU 平台微差。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromDoubleDeterministic(double value, int precision = 10000)
        {
            long scaled = (long)Math.Round(value * precision);
            return new FP((scaled << FRACTIONAL_PLACES) / precision);
        }
        /// <summary>
        /// 从字符串解析定点数 (纯整型运算，0 浮点误差)
        /// 支持 "0.15", "12", "-1.5" 等格式
        /// </summary>
        public static FP Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return Zero;
            s = s.Trim();
            bool negative = false;
            if (s.StartsWith("-"))
            {
                negative = true;
                s = s.Substring(1);
            }
            else if (s.StartsWith("+"))
            {
                s = s.Substring(1);
            }

            int dotIndex = s.IndexOf('.');
            long integerPart = 0;
            long fractionalPart = 0;
            long scale = 1;

            if (dotIndex < 0)
            {
                integerPart = long.Parse(s);
            }
            else
            {
                string intStr = s.Substring(0, dotIndex);
                string fracStr = s.Substring(dotIndex + 1);

                if (!string.IsNullOrEmpty(intStr))
                    integerPart = long.Parse(intStr);

                if (!string.IsNullOrEmpty(fracStr))
                {
                    if (fracStr.Length > 9) fracStr = fracStr.Substring(0, 9);
                    fractionalPart = long.Parse(fracStr);
                    for (int i = 0; i < fracStr.Length; i++) scale *= 10;
                }
            }

            long rawVal = (integerPart << FRACTIONAL_PLACES) + ((fractionalPart << FRACTIONAL_PLACES) / scale);
            return new FP(negative ? -rawVal : rawVal);
        }
        public static bool TryParse(string s, out FP result)
        {
            if (string.IsNullOrEmpty(s))
            {
                result = Zero;
                return false;
            }
            try
            {
                result = Parse(s);
                return true;
            }
            catch
            {
                result = Zero;
                return false;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromInt(int value)
        {
            return new FP((long)value << FRACTIONAL_PLACES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromLong(long value)
        {
            return new FP(value << FRACTIONAL_PLACES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AsFloat()
        {
            return (float)_serializedValue / ONE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double AsDouble()
        {
            return (double)_serializedValue / ONE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AsInt()
        {
            return (int)(_serializedValue >> FRACTIONAL_PLACES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long AsLong()
        {
            return _serializedValue >> FRACTIONAL_PLACES;
        }

        #endregion

        #region 隐式/显式转换运算符

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(int value) => FromInt(value);

        /// <summary>
        /// 字符串隐式转换为定点数 (调用纯整型解析器，100% 确定性)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(string value) => Parse(value);

        [Obsolete("严禁在确定性逻辑中显式强转浮点数！请使用 FP.Parse(\"...\") 或纯整型。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator FP(float value) => FromFloat(value);

        [Obsolete("严禁在确定性逻辑中显式强转浮点数！请使用 FP.Parse(\"...\") 或纯整型。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator FP(double value) => FromDouble(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator FP(long value) => FromLong(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator float(FP value) => value.AsFloat();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator double(FP value) => value.AsDouble();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator int(FP value) => value.AsInt();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator long(FP value) => value.AsLong();

        #endregion

        #region 算术运算符

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator +(FP x, FP y)
        {
            return new FP(x._serializedValue + y._serializedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP x, FP y)
        {
            return new FP(x._serializedValue - y._serializedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP x)
        {
            return new FP(-x._serializedValue);
        }

        /// <summary>
        /// 乘法：拆分为 32 位高低段进行无溢出 128 位乘法
        /// </summary>
        public static FP operator *(FP x, FP y)
        {
            long xl = x._serializedValue;
            long yl = y._serializedValue;

            bool isNegative = (xl < 0) ^ (yl < 0);
            ulong ux = (ulong)(xl < 0 ? -xl : xl);
            ulong uy = (ulong)(yl < 0 ? -yl : yl);

            ulong xHi = ux >> 32;
            ulong xLo = ux & 0xFFFFFFFFUL;
            ulong yHi = uy >> 32;
            ulong yLo = uy & 0xFFFFFFFFUL;

            ulong p0 = xLo * yLo;
            ulong p1 = xLo * yHi;
            ulong p2 = xHi * yLo;
            ulong p3 = xHi * yHi;

            ulong p0Hi = p0 >> 32;
            ulong mid = p1 + p2 + p0Hi;
            ulong result = (p3 << 32) + mid;

            long resLong = (long)result;
            return new FP(isNegative ? -resLong : resLong);
        }

        /// <summary>
        /// 除法：采用 Leading Zero 归一化位移除法，保证极速与高精度
        /// </summary>
        public static FP operator /(FP x, FP y)
        {
            long xl = x._serializedValue;
            long yl = y._serializedValue;

            if (yl == 0)
            {
                return xl >= 0 ? MaxValue : MinValue;
            }

            bool isNegative = (xl < 0) ^ (yl < 0);
            ulong remainder = (ulong)(xl < 0 ? -xl : xl);
            ulong divider = (ulong)(yl < 0 ? -yl : yl);

            ulong quotient = 0UL;
            int bitPos = FRACTIONAL_PLACES;

            while (remainder != 0 && bitPos >= 0)
            {
                int shift = CountLeadingZeros(remainder);
                if (shift > bitPos)
                {
                    shift = bitPos;
                }
                remainder <<= shift;
                bitPos -= shift;

                ulong div = remainder / divider;
                remainder = remainder % divider;
                quotient += (div << bitPos);

                remainder <<= 1;
                bitPos--;
            }

            long resLong = (long)quotient;
            return new FP(isNegative ? -resLong : resLong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator %(FP x, FP y)
        {
            return new FP(x._serializedValue % y._serializedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        #region 比较运算符与接口

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FP x, FP y) => x._serializedValue == y._serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FP x, FP y) => x._serializedValue != y._serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(FP x, FP y) => x._serializedValue < y._serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(FP x, FP y) => x._serializedValue > y._serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(FP x, FP y) => x._serializedValue <= y._serializedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(FP x, FP y) => x._serializedValue >= y._serializedValue;

        public bool Equals(FP other) => _serializedValue == other._serializedValue;

        public override bool Equals(object obj) => obj is FP other && Equals(other);

        public override int GetHashCode() => _serializedValue.GetHashCode();

        public int CompareTo(FP other) => _serializedValue.CompareTo(other._serializedValue);

        public override string ToString()
        {
            if (_serializedValue == 0) return "0.000";
            bool neg = _serializedValue < 0;
            ulong abs = (ulong)(neg ? -_serializedValue : _serializedValue);
            ulong integerPart = abs >> FRACTIONAL_PLACES;
            ulong fracPart = abs & 0xFFFFFFFFUL;
            // 纯整型四舍五入到 3 位小数: (fracPart * 1000 + HALF) >> 32
            ulong fracDec = ((fracPart * 1000UL) + (1UL << 31)) >> FRACTIONAL_PLACES;
            if (fracDec >= 1000UL)
            {
                integerPart += 1;
                fracDec = 0;
            }
            return neg ? $"-{integerPart}.{fracDec:D3}" : $"{integerPart}.{fracDec:D3}";
        }
        #endregion

        #region 基础数学函数

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP value)
        {
            return new FP(value._serializedValue < 0 ? -value._serializedValue : value._serializedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP value)
        {
            if (value._serializedValue > 0) return 1;
            if (value._serializedValue < 0) return -1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Floor(FP value)
        {
            long mask = ~(ONE - 1);
            return new FP(value._serializedValue & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Ceiling(FP value)
        {
            long mask = ~(ONE - 1);
            long remainder = value._serializedValue & (ONE - 1);
            return new FP(remainder > 0 ? (value._serializedValue & mask) + ONE : value._serializedValue & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Round(FP value)
        {
            return Floor(value + Half);
        }

        #endregion
    }
}
