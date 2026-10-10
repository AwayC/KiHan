using System;
using System.Runtime.CompilerServices;
using TrueSync;
using UnityEngine;

namespace KiHan.Math
{
    /// <summary>
    /// 项目统一二维定点数向量门面
    /// 包含面向 3D 纵深渲染的坐标投影辅助方法 (.ToRenderWorldPos())
    /// </summary>
    [Serializable]
    public struct FPVector2 : IEquatable<FPVector2>
    {
        public FP x;
        public FP y;

        public static readonly FPVector2 zero = new FPVector2(FP.Zero, FP.Zero);
        public static readonly FPVector2 one = new FPVector2(FP.One, FP.One);
        public static readonly FPVector2 right = new FPVector2(FP.One, FP.Zero);
        public static readonly FPVector2 left = new FPVector2(-FP.One, FP.Zero);
        public static readonly FPVector2 up = new FPVector2(FP.Zero, FP.One);
        public static readonly FPVector2 down = new FPVector2(FP.Zero, -FP.One);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(FP x, FP y)
        {
            this.x = x;
            this.y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(int x, int y)
        {
            this.x = FP.FromInt(x);
            this.y = FP.FromInt(y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(string x, string y)
        {
            this.x = FP.Parse(x);
            this.y = FP.Parse(y);
        }


        public FP sqrMagnitude => x * x + y * y;

        public FP magnitude => TSMath.Sqrt(sqrMagnitude);

        public FPVector2 normalized
        {
            get
            {
                FP mag = magnitude;
                if (mag > FP.Epsilon)
                {
                    return this / mag;
                }
                return zero;
            }
        }

        /// <summary>
        /// 从 Unity Vector2 转换 (仅供表现层/离线工具使用，确定性逻辑请使用 new FPVector2("x", "y"))
        /// </summary>
        [Obsolete("严禁在确定性逻辑中使用 Vector2 浮点转换！请使用 new FPVector2(string x, string y)。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 FromVector2Deterministic(Vector2 v, int precision = 10000)
        {
            return new FPVector2(
                FP.FromFloatDeterministic(v.x, precision),
                FP.FromFloatDeterministic(v.y, precision)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Normalize()
        {
            FP mag = magnitude;
            if (mag > FP.Epsilon)
            {
                x /= mag;
                y /= mag;
            }
            else
            {
                x = FP.Zero;
                y = FP.Zero;
            }
        }

        #region 表现层转换方法 (仅供 View/渲染层单向读取)

        /// <summary>
        /// 转换为 Unity 2D 坐标 (X -> X, Y -> Y)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ToVector2()
        {
            return new Vector2(x.AsFloat(), y.AsFloat());
        }

        /// <summary>
        /// 转换为 3D 渲染世界坐标: X -> World.X, Height -> World.Y, Y(纵深) -> World.Z
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToRenderWorldPos(float heightMeters = 0f)
        {
            return new Vector3(x.AsFloat(), heightMeters, y.AsFloat());
        }

        /// <summary>
        /// 转换为 3D 渲染世界坐标 (使用定点数高度)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToRenderWorldPos(FP heightMeters)
        {
            return new Vector3(x.AsFloat(), heightMeters.AsFloat(), y.AsFloat());
        }

        #endregion

        #region 数学运算

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FPVector2 a, FPVector2 b) => a.x * b.x + a.y * b.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(FPVector2 a, FPVector2 b) => (a - b).magnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP SqrDistance(FPVector2 a, FPVector2 b) => (a - b).sqrMagnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Lerp(FPVector2 a, FPVector2 b, FP t)
        {
            t = TSMath.Clamp01(t);
            return new FPVector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 MoveTowards(FPVector2 current, FPVector2 target, FP maxDistanceDelta)
        {
            FPVector2 to = target - current;
            FP dist = to.magnitude;
            if (dist <= maxDistanceDelta || dist < FP.Epsilon) return target;
            return current + to / dist * maxDistanceDelta;
        }

        #endregion

        #region 运算符与互转

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator +(FPVector2 a, FPVector2 b) => new FPVector2(a.x + b.x, a.y + b.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 a, FPVector2 b) => new FPVector2(a.x - b.x, a.y - b.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 a) => new FPVector2(-a.x, -a.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FPVector2 a, FP d) => new FPVector2(a.x * d, a.y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FP d, FPVector2 a) => new FPVector2(a.x * d, a.y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator /(FPVector2 a, FP d) => new FPVector2(a.x / d, a.y / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector2 a, FPVector2 b) => a.x == b.x && a.y == b.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector2 a, FPVector2 b) => !(a == b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TSVector2(FPVector2 v) => new TSVector2(v.x, v.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FPVector2(TSVector2 v) => new FPVector2(v.x, v.y);

        public bool Equals(FPVector2 other) => x == other.x && y == other.y;

        public override bool Equals(object obj) => obj is FPVector2 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

        public override string ToString() => $"({x}, {y})";

        #endregion
    }

    /// <summary>
    /// 项目统一三维定点数向量门面
    /// </summary>
    [Serializable]
    public struct FPVector3 : IEquatable<FPVector3>
    {
        public FP x;
        public FP y;
        public FP z;

        public static readonly FPVector3 zero = new FPVector3(FP.Zero, FP.Zero, FP.Zero);
        public static readonly FPVector3 one = new FPVector3(FP.One, FP.One, FP.One);
        public static readonly FPVector3 right = new FPVector3(FP.One, FP.Zero, FP.Zero);
        public static readonly FPVector3 left = new FPVector3(-FP.One, FP.Zero, FP.Zero);
        public static readonly FPVector3 up = new FPVector3(FP.Zero, FP.One, FP.Zero);
        public static readonly FPVector3 down = new FPVector3(FP.Zero, -FP.One, FP.Zero);
        public static readonly FPVector3 forward = new FPVector3(FP.Zero, FP.Zero, FP.One);
        public static readonly FPVector3 back = new FPVector3(FP.Zero, FP.Zero, -FP.One);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(FP x, FP y, FP z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(string x, string y, string z)
        {
            this.x = FP.Parse(x);
            this.y = FP.Parse(y);
            this.z = FP.Parse(z);
        }


        public FP sqrMagnitude => x * x + y * y + z * z;

        public FP magnitude => TSMath.Sqrt(sqrMagnitude);

        public FPVector3 normalized
        {
            get
            {
                FP mag = magnitude;
                if (mag > FP.Epsilon) return this / mag;
                return zero;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToVector3()
        {
            return new Vector3(x.AsFloat(), y.AsFloat(), z.AsFloat());
        }
        /// <summary>
        /// 从 Unity Vector3 转换 (仅供表现层/离线工具使用，确定性逻辑请使用 new FPVector3("x", "y", "z"))
        /// </summary>
        [Obsolete("严禁在确定性逻辑中使用 Vector3 浮点转换！请使用 new FPVector3(string x, string y, string z)。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 FromVector3Deterministic(Vector3 v, int precision = 10000)
        {
            return new FPVector3(
                FP.FromFloatDeterministic(v.x, precision),
                FP.FromFloatDeterministic(v.y, precision),
                FP.FromFloatDeterministic(v.z, precision)
            );
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FPVector3 a, FPVector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Cross(FPVector3 a, FPVector3 b)
        {
            return new FPVector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(FPVector3 a, FPVector3 b) => (a - b).magnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Lerp(FPVector3 a, FPVector3 b, FP t)
        {
            t = TSMath.Clamp01(t);
            return new FPVector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator +(FPVector3 a, FPVector3 b) => new FPVector3(a.x + b.x, a.y + b.y, a.z + b.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(FPVector3 a, FPVector3 b) => new FPVector3(a.x - b.x, a.y - b.y, a.z - b.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(FPVector3 a) => new FPVector3(-a.x, -a.y, -a.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FPVector3 a, FP d) => new FPVector3(a.x * d, a.y * d, a.z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FP d, FPVector3 a) => new FPVector3(a.x * d, a.y * d, a.z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator /(FPVector3 a, FP d) => new FPVector3(a.x / d, a.y / d, a.z / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector3 a, FPVector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector3 a, FPVector3 b) => !(a == b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TSVector3(FPVector3 v) => new TSVector3(v.x, v.y, v.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FPVector3(TSVector3 v) => new FPVector3(v.x, v.y, v.z);

        public bool Equals(FPVector3 other) => x == other.x && y == other.y && z == other.z;

        public override bool Equals(object obj) => obj is FPVector3 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);

        public override string ToString() => $"({x}, {y}, {z})";
    }

    /// <summary>
    /// 项目统一定点数数学工具类 (直接门面转发 TSMath)
    /// </summary>
    public static class FPMath
    {
        public static readonly FP Pi = TSMath.Pi;
        public static readonly FP TwoPi = TSMath.TwoPi;
        public static readonly FP HalfPi = TSMath.HalfPi;
        public static readonly FP Deg2Rad = TSMath.Deg2Rad;
        public static readonly FP Rad2Deg = TSMath.Rad2Deg;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Min(FP a, FP b) => TSMath.Min(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Max(FP a, FP b) => TSMath.Max(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp(FP v, FP min, FP max) => TSMath.Clamp(v, min, max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp01(FP v) => TSMath.Clamp01(v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP v) => TSMath.Abs(v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP v) => TSMath.Sign(v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Sqrt(FP v) => TSMath.Sqrt(v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Sin(FP rad) => TSMath.Sin(rad);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Cos(FP rad) => TSMath.Cos(rad);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Tan(FP rad) => TSMath.Tan(rad);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Atan2(FP y, FP x) => TSMath.Atan2(y, x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Lerp(FP from, FP to, FP t) => TSMath.Lerp(from, to, t);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP MoveTowards(FP cur, FP target, FP maxDelta) => TSMath.MoveTowards(cur, target, maxDelta);
    }

    /// <summary>
    /// 定点数与 Unity 类型之间的安全扩展方法
    /// </summary>
    public static class FPExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat(this FP fp) => fp.AsFloat();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt(this FP fp) => fp.AsInt();

        [Obsolete("严禁在确定性逻辑中将 float 转换为 FP！请使用 FP.Parse 或整数类型。")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP ToFP(this float val) => FP.FromFloat(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP ToFP(this int val) => FP.FromInt(val);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP ToFP(this string str) => FP.Parse(str);

        [Obsolete("严禁在确定性逻辑中使用 Vector2 转 FPVector2！")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 ToFPVector2(this Vector2 v) => new FPVector2(FP.FromFloat(v.x), FP.FromFloat(v.y));

        [Obsolete("严禁在确定性逻辑中使用 Vector3 转 FPVector3！")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 ToFPVector3(this Vector3 v) => new FPVector3(FP.FromFloat(v.x), FP.FromFloat(v.y), FP.FromFloat(v.z));
    }
}
