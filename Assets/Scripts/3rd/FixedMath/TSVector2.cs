using System;
using System.Runtime.CompilerServices;

namespace TrueSync
{
    /// <summary>
    /// 定点数二维向量
    /// </summary>
    [Serializable]
    public struct TSVector2 : IEquatable<TSVector2>
    {
        public FP x;
        public FP y;

        public static readonly TSVector2 zero = new TSVector2(FP.Zero, FP.Zero);
        public static readonly TSVector2 one = new TSVector2(FP.One, FP.One);
        public static readonly TSVector2 right = new TSVector2(FP.One, FP.Zero);
        public static readonly TSVector2 left = new TSVector2(-FP.One, FP.Zero);
        public static readonly TSVector2 up = new TSVector2(FP.Zero, FP.One);
        public static readonly TSVector2 down = new TSVector2(FP.Zero, -FP.One);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TSVector2(FP x, FP y)
        {
            this.x = x;
            this.y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TSVector2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public FP sqrMagnitude => x * x + y * y;

        public FP magnitude => TSMath.Sqrt(sqrMagnitude);

        public TSVector2 normalized
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(TSVector2 a, TSVector2 b)
        {
            return a.x * b.x + a.y * b.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(TSVector2 a, TSVector2 b)
        {
            return (a - b).magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP SqrDistance(TSVector2 a, TSVector2 b)
        {
            return (a - b).sqrMagnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 Lerp(TSVector2 a, TSVector2 b, FP t)
        {
            t = TSMath.Clamp01(t);
            return new TSVector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 MoveTowards(TSVector2 current, TSVector2 target, FP maxDistanceDelta)
        {
            TSVector2 toVector = target - current;
            FP dist = toVector.magnitude;
            if (dist <= maxDistanceDelta || dist < FP.Epsilon)
            {
                return target;
            }
            return current + toVector / dist * maxDistanceDelta;
        }

        #region 运算符重载

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 operator +(TSVector2 a, TSVector2 b) => new TSVector2(a.x + b.x, a.y + b.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 operator -(TSVector2 a, TSVector2 b) => new TSVector2(a.x - b.x, a.y - b.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 operator -(TSVector2 a) => new TSVector2(-a.x, -a.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 operator *(TSVector2 a, FP d) => new TSVector2(a.x * d, a.y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 operator *(FP d, TSVector2 a) => new TSVector2(a.x * d, a.y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector2 operator /(TSVector2 a, FP d) => new TSVector2(a.x / d, a.y / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TSVector2 a, TSVector2 b) => a.x == b.x && a.y == b.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TSVector2 a, TSVector2 b) => !(a == b);

        public bool Equals(TSVector2 other) => x == other.x && y == other.y;

        public override bool Equals(object obj) => obj is TSVector2 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

        public override string ToString() => $"({x}, {y})";

        #endregion
    }
}
