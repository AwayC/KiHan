using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 定点数世界轴对齐包围盒 (x: 横向, y: 纵深, z: 高度)
    /// </summary>
    public struct FPBox
    {
        public FPVector3 Min;
        public FPVector3 Max;

        public FPBox(FPVector3 min, FPVector3 max)
        {
            Min = min;
            Max = max;
        }

        public FPVector3 Center => (Min + Max) * FP.Half;

        /// <summary>
        /// 三轴区间重叠检测 (边缘接触视为命中)
        /// </summary>
        public bool Overlaps(FPBox other)
        {
            return Min.x <= other.Max.x && other.Min.x <= Max.x &&
                   Min.y <= other.Max.y && other.Min.y <= Max.y &&
                   Min.z <= other.Max.z && other.Min.z <= Max.z;
        }

        /// <summary>
        /// 两盒重叠区域的中心点 (用于打击火花定位)，调用前需保证 Overlaps 为 true
        /// </summary>
        public FPVector3 IntersectionCenter(FPBox other)
        {
            FPVector3 min = new FPVector3(FPMath.Max(Min.x, other.Min.x), FPMath.Max(Min.y, other.Min.y), FPMath.Max(Min.z, other.Min.z));
            FPVector3 max = new FPVector3(FPMath.Min(Max.x, other.Max.x), FPMath.Min(Max.y, other.Max.y), FPMath.Min(Max.z, other.Max.z));
            return (min + max) * FP.Half;
        }
    }
}
