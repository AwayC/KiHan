using System;
using System.Runtime.CompilerServices;

namespace TrueSync
{
    /// <summary>
    /// 定点数三维向量
    /// </summary>
    [Serializable]
    public struct TSVector3 : IEquatable<TSVector3>
    {
        public FP x;
        public FP y;
        public FP z;

        public static readonly TSVector3 zero = new TSVector3(FP.Zero, FP.Zero, FP.Zero);
        public static readonly TSVector3 one = new TSVector3(FP.One, FP.One, FP.One);
        public static readonly TSVector3 right = new TSVector3(FP.One, FP.Zero, FP.Zero);
        public static readonly TSVector3 left = new TSVector3(-FP.One, FP.Zero, FP.Zero);
        public static readonly TSVector3 up = new TSVector3(FP.Zero, FP.One, FP.Zero);
        public static readonly TSVector3 down = new TSVector3(FP.Zero, -FP.One, FP.Zero);
        public static readonly TSVector3 forward = new TSVector3(FP.Zero, FP.Zero, FP.One);
        public static readonly TSVector3 back = new TSVector3(FP.Zero, FP.Zero, -FP.One);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TSVector3(FP x, FP y, FP z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TSVector3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public FP sqrMagnitude => x * x + y * y + z * z;

        public FP magnitude => TSMath.Sqrt(sqrMagnitude);

        public TSVector3 normalized
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
                z /= mag;
            }
            else
            {
                x = FP.Zero;
                y = FP.Zero;
                z = FP.Zero;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(TSVector3 a, TSVector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 Cross(TSVector3 a, TSVector3 b)
        {
            return new TSVector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(TSVector3 a, TSVector3 b)
        {
            return (a - b).magnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP SqrDistance(TSVector3 a, TSVector3 b)
        {
            return (a - b).sqrMagnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 Lerp(TSVector3 a, TSVector3 b, FP t)
        {
            t = TSMath.Clamp01(t);
            return new TSVector3(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t
            );
        }

        #region 运算符重载

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 operator +(TSVector3 a, TSVector3 b) => new TSVector3(a.x + b.x, a.y + b.y, a.z + b.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 operator -(TSVector3 a, TSVector3 b) => new TSVector3(a.x - b.x, a.y - b.y, a.z - b.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 operator -(TSVector3 a) => new TSVector3(-a.x, -a.y, -a.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 operator *(TSVector3 a, FP d) => new TSVector3(a.x * d, a.y * d, a.z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 operator *(FP d, TSVector3 a) => new TSVector3(a.x * d, a.y * d, a.z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSVector3 operator /(TSVector3 a, FP d) => new TSVector3(a.x / d, a.y / d, a.z / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TSVector3 a, TSVector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TSVector3 a, TSVector3 b) => !(a == b);

        public bool Equals(TSVector3 other) => x == other.x && y == other.y && z == other.z;

        public override bool Equals(object obj) => obj is TSVector3 other && Equals(other);

        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);

        public override string ToString() => $"({x}, {y}, {z})";

        #endregion
    }
}
