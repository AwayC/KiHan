using TrueSync;
using KiHan.Math;
using KiHan.Logic;

/// <summary>
/// 地图逻辑边界 (纯定点数，String-First 配置)
/// 当前墙体仅为地图四周的边界墙：X 轴左右为墙，Y 轴为决斗场纵深上下限
/// </summary>
public class MapLogic
{
    public FP MinX = FP.Parse("-7");
    public FP MaxX = FP.Parse("7");
    public FP MinY = FP.Parse("-0.9");
    public FP MaxY = FP.Parse("0.9");

    /// <summary>
    /// 将逻辑坐标的水平面 (x 横向, y 纵深) 夹取到地图边界内，返回撞墙事件
    /// </summary>
    public PhysicsEvent ClampPlanar(ref FPVector3 pos)
    {
        PhysicsEvent evt = PhysicsEvent.None;

        if (pos.x < MinX)
        {
            pos.x = MinX;
            evt |= PhysicsEvent.HitWallLeft;
        }
        else if (pos.x > MaxX)
        {
            pos.x = MaxX;
            evt |= PhysicsEvent.HitWallRight;
        }

        pos.y = FPMath.Clamp(pos.y, MinY, MaxY);
        return evt;
    }
}
