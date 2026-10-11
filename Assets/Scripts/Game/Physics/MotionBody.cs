using System;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 单帧物理事件 (可组合)
    /// </summary>
    [Flags]
    public enum PhysicsEvent
    {
        None = 0,
        /// <summary>本帧从空中落地</summary>
        Landed = 1 << 0,
        /// <summary>本帧从地面离开</summary>
        LeftGround = 1 << 1,
        /// <summary>本帧撞到左边界墙</summary>
        HitWallLeft = 1 << 2,
        /// <summary>本帧撞到右边界墙</summary>
        HitWallRight = 1 << 3,
    }

    /// <summary>
    /// 角色确定性运动体 (纯定点数)
    /// 坐标约定：x 横向，y 决斗场纵深，z 离地高度
    /// 表现层映射：World = (x, z, y)
    ///
    /// 运动规则：
    /// 1. 水平面 (x, y)：由曲线位移驱动 (RootMotion 曲线 / ForceMove / 摇杆输入)，只受地图边界墙约束
    /// 2. 竖直 (z)：不使用任何曲线，只有速度模型
    ///    - 起跳/击飞：直接设置竖直速度 (Launch)
    ///    - 动作附加加速度 (RootMotionSegment.accelerationZ) 与重力合成：a = 附加加速度 - 重力 × GravityScale
    ///    - 按匀加速精确积分 z += v·dt + ½a·dt²，v += a·dt
    /// 3. 高度触底且速度向下即落地，产生 Landed 事件
    /// 角色之间不做碰撞
    /// </summary>
    public class MotionBody
    {
        /// <summary>
        /// 贴地容差 (0.1 像素)：吸收逻辑帧步长 (1/LOGIC_FPS 秒) 的定点数舍入残差，避免理论上已触地却多滞空一帧
        /// </summary>
        public static readonly FP GroundEpsilon = FP.Parse("0.001");

        public FPVector3 Position = FPVector3.zero;

        /// <summary>
        /// 竖直速度 (单位/秒，向上为正)，供击飞等需要初速度的场景使用
        /// </summary>
        public FP VelocityZ = FP.Zero;

        /// <summary>
        /// 重力倍率 (保护机制加重下坠等)
        /// </summary>
        public FP GravityScale = FP.One;

        /// <summary>
        /// 是否滞空 (在空中)
        /// </summary>
        public bool IsAirborne => Position.z > FP.Zero || VelocityZ > FP.Zero;

        public bool IsGrounded => !IsAirborne;

        /// <summary>
        /// 推进一个逻辑帧
        /// </summary>
        /// <param name="planarDelta">本帧水平面位移 (x 横向, y 纵深)，z 分量忽略</param>
        /// <param name="extraAccelerationZ">本帧动作附加的竖直加速度 (单位/秒²，向上为正)</param>
        /// <param name="gravity">重力加速度 (单位/秒²)</param>
        /// <param name="dt">逻辑帧时长</param>
        /// <param name="map">地图边界，可为 null</param>
        public PhysicsEvent Step(FPVector3 planarDelta, FP extraAccelerationZ, FP gravity, FP dt, MapLogic map)
        {
            bool wasAirborne = IsAirborne;
            bool wasOnGround = Position.z <= FP.Zero;
            FPVector3 pos = Position;

            pos.x += planarDelta.x;
            pos.y += planarDelta.y;

            // 合加速度 = 动作附加加速度 - 重力；地面上只有合加速度向上时才会离地
            FP accel = extraAccelerationZ - gravity * GravityScale;
            if (wasAirborne || accel > FP.Zero)
            {
                // 匀加速精确积分：大步长下抛物线采样点与解析解一致，起跳高度 = v²/2g
                pos.z += VelocityZ * dt + accel * dt * dt * FP.Half;
                VelocityZ += accel * dt;
            }

            PhysicsEvent evt = PhysicsEvent.None;

            if (pos.z <= GroundEpsilon && VelocityZ <= FP.Zero)
            {
                pos.z = FP.Zero;
                VelocityZ = FP.Zero;
                if (!wasOnGround) evt |= PhysicsEvent.Landed;
            }
            else if (wasOnGround)
            {
                evt |= PhysicsEvent.LeftGround;
            }

            if (map != null) evt |= map.ClampPlanar(ref pos);

            Position = pos;
            return evt;
        }

        /// <summary>
        /// 赋予竖直初速度 (击飞/弹起)
        /// </summary>
        public void Launch(FP velocityZ)
        {
            VelocityZ = velocityZ;
        }

        public void Reset(FPVector3 position)
        {
            Position = position;
            VelocityZ = FP.Zero;
            GravityScale = FP.One;
        }
    }
}
