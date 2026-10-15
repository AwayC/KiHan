using System;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// RootMotion 分段 (一个动作可配置多段，每段按自己的动画帧区间生效，30fps 素材帧，闭区间)
    ///
    /// 水平面 (x 面朝方向前方, y 纵深)：本段总位移 (moveX, moveY) 按 tweenMethod 缓动分配到 frameRange 内，
    ///   区间内每个动画帧推进 1 / 区间帧数 的进度，多段同帧重叠时位移相加
    /// 竖直 (z 高度)：不使用曲线，只用速度模型
    ///   - velocityZ：在 frameRange.x 帧把竖直速度设置为该值 (起跳 / 二段跳 / 下砸；"0" 表示清零竖直速度)
    ///   - accelerationZ：在 frameRange 内附加竖直加速度，与重力合成后积分 (= 重力即抵消重力，顶点处即悬停；> 重力即向上推升)
    /// 水平惯性：velocityX / velocityY 在 frameRange.x 帧设置水平惯性速度 (x 面朝方向前方, y 纵深；"0" 表示清零)，
    ///   之后与动画帧无关地持续移动，直到切换到其他动画、落地或撞到边界 (击飞流程中保留，见 CharacterObj)
    /// </summary>
    [Serializable]
    public struct RootMotionSegment
    {
        public FrameRange frameRange;

        /// <summary>
        /// 本段水平总位移 x (面朝方向前方为正，随朝向镜像)
        /// </summary>
        public FP moveX;

        /// <summary>
        /// 本段水平总位移 y (纵深)
        /// </summary>
        public FP moveY;

        /// <summary>
        /// 水平位移缓动曲线名称 (ForceMoveMethod：Linear / Slowly)，为空时为 Linear
        /// </summary>
        public string tweenMethod;

        /// <summary>
        /// 是否在 frameRange.x 帧设置竖直速度
        /// </summary>
        public bool hasVelocityZ;

        /// <summary>
        /// 设置的竖直速度 (单位/秒，向上为正，负值即下砸)
        /// </summary>
        public FP velocityZ;

        /// <summary>
        /// 附加竖直加速度 (单位/秒²，向上为正)，逻辑帧只覆盖区间一部分时按覆盖帧数折算
        /// </summary>
        public FP accelerationZ;

        /// <summary>
        /// 是否在 frameRange.x 帧设置水平惯性速度
        /// </summary>
        public bool hasPlanarVelocity;

        /// <summary>
        /// 设置的水平惯性速度 (单位/秒；x 面朝方向前方为正，随朝向镜像；y 纵深)
        /// </summary>
        public FP velocityX;
        public FP velocityY;

        public bool HasMove => moveX != FP.Zero || moveY != FP.Zero;

        /// <summary>
        /// 处理完 frame 个动画帧时，本段水平位移的累计缓动进度 [0, 1]
        /// </summary>
        public FP MoveProgress(int frame)
        {
            int span = frameRange.y - frameRange.x + 1;
            FP t;
            if (span <= 0)
            {
                t = frame > frameRange.x ? FP.One : FP.Zero;
            }
            else
            {
                int done = frame - frameRange.x;
                t = done <= 0 ? FP.Zero : done >= span ? FP.One : (FP)done / span;
            }
            return ForceMoveMethod.Get(tweenMethod)(t);
        }
    }
}
