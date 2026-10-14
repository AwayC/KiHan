using KiHan.Logic;
using KiHan.Math;
using UnityEngine;

namespace KiHan.View.Render
{
    /// <summary>
    /// 战斗表现空间：逻辑坐标 (x 横向, y 纵深, z 高度) 到 Unity 世界坐标的唯一映射
    ///
    /// 相机为不旋转的正交相机 (沿世界 +Z 观察)，角色、特效、地图等 2D 素材同样不旋转，正立显示。
    /// 映射公式：World = (x, y·k + z, y)，k = DepthToScreenY
    /// - 屏幕横坐标 = x，屏幕纵坐标 = 纵深 y × k + 高度 z
    /// - 世界 Z = 纵深 y，只用于深度缓冲裁决前后 (纵深越大越远)；高度 z 只抬升屏幕纵坐标，跳跃不改变遮挡关系
    /// </summary>
    public static class BattleViewSpace
    {
        /// <summary>
        /// 纵深 y 投到屏幕纵坐标上的比例 (原版实测值，勿随意修改)：纵深每走 1 单位，画面上移 k 单位 (高度 z 为 1:1)。
        /// 与脚下阴影圆盘的纵向压缩 (CharacterFootShadow) 共用：地面上的圆与地面上的位移按同一比例压缩
        /// </summary>
        public const float DepthToScreenY = 0.368f;

        /// <summary>
        /// 角色表现缩放 (精灵、额外图层、跟随特效、脚下阴影)；只影响画面，不影响逻辑判定盒与位移
        /// </summary>
        public const float CharacterScale = 1f;

        /// <summary>
        /// 相机与纵深原点平面的距离 (正交相机只影响裁剪，不影响画面大小)
        /// </summary>
        public const float CameraDistance = 20f;

        /// <summary>
        /// 地图背景板放在所有角色之后的世界 Z
        /// </summary>
        public const float BackdropDistance = 60f;

        /// <summary>
        /// 地图背景板的 sortingOrder 偏移：透明队列中背景必须最先绘制，否则不写深度的特效会被背景覆盖
        /// </summary>
        public const int BackdropSortingOffset = -1000;

        /// <summary>
        /// 打击火花、跳字等前景表现朝相机 (-Z) 前移的距离，保证位于参与命中的双方角色之前
        /// </summary>
        public const float ForegroundOffset = 1.5f;

        /// <summary>
        /// 帧库像素 -> 世界单位
        /// </summary>
        public const float PixelToWorld = 1f / GameConfig.PIXELS_PER_UNIT;

        /// <summary>
        /// 逻辑坐标 (x, y 纵深, z 高度) -> 世界坐标 的线性变换矩阵 (用于 Gizmos 等需要整体变换的场合)
        /// </summary>
        public static readonly Matrix4x4 LogicToWorldMatrix = new Matrix4x4(
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(0f, DepthToScreenY, 1f, 0f),
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        /// <summary>
        /// 地面锚点：逻辑 (x, y 纵深) 在高度 0 处的世界坐标
        /// </summary>
        public static Vector3 GroundToWorld(float x, float depth) => new Vector3(x, depth * DepthToScreenY, depth);

        public static Vector3 ToWorld(float x, float depth, float height) => new Vector3(x, depth * DepthToScreenY + height, depth);

        public static Vector3 ToWorld(FPVector3 logic) => ToWorld(logic.x.AsFloat(), logic.y.AsFloat(), logic.z.AsFloat());

        /// <summary>
        /// 相机位置：画面中心对准横坐标 x、屏幕纵坐标 screenY (世界 Y)
        /// </summary>
        public static Vector3 CameraPosition(float x, float screenY) => new Vector3(x, screenY, -CameraDistance);
    }
}
