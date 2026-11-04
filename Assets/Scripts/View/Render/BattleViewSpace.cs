using KiHan.Logic;
using KiHan.Math;
using UnityEngine;

namespace KiHan.View.Render
{
    /// <summary>
    /// 战斗表现空间：逻辑坐标 (x 横向, y 纵深, z 高度) 到 Unity 世界坐标的唯一映射
    ///
    /// 相机为正交相机 (沿世界 +Z 观察，只在演出时绕视线轴旋转)，角色、特效、地图等 2D 素材不旋转，正立显示。
    /// 纵深 y 与原版一致使用视觉单位 (脚底在画面上的纵坐标)，映射公式：World = (x, y + z, y / k)，k = DepthToScreenY
    /// - 屏幕横坐标 = x，屏幕纵坐标 = 纵深 y + 高度 z
    /// - 世界 Z = y / k (换算回地面距离)，只用于深度缓冲裁决前后 (纵深越大越远)；高度 z 只抬升屏幕纵坐标，跳跃不改变遮挡关系
    /// </summary>
    public static class BattleViewSpace
    {
        /// <summary>
        /// 地面透视压缩比 (原版实测值，勿随意修改)：地面上 1 单位的前后距离在画面上显示为 k 单位。
        /// 用于脚下阴影圆盘的纵向压缩 (CharacterFootShadow) 与世界 Z 的换算；逻辑纵深本身已是视觉单位，
        /// 纵深移动速度 = 横向速度 × k (CharacterObj.moveSpeedY)
        /// </summary>
        public const float DepthToScreenY = 0.368f;

        /// <summary>
        /// 角色表现缩放 (精灵、额外图层、跟随特效、脚下阴影)；只影响画面，不影响逻辑判定盒与位移
        /// </summary>
        public const float CharacterScale = 1f;

        /// <summary>
        /// 地图背景板与相机取景的放大倍数。原版战斗中角色显示为原像素的 0.875 (= 7/8) 倍；
        /// 这里保持角色 1 像素 = 0.01 单位 (与原版判定盒、位移等逻辑数据一致)，改为把地图放大 8/7，
        /// 相机取景 (正交尺寸、画面中心高度、横向边界、前视距离) 同比例放大，画面中地图大小不变、角色相对缩小。
        /// 逻辑地图边界 (MapLogic) 按同一倍数写定 (逻辑层不引用表现层常量，修改时需同步)
        /// </summary>
        public const float MapScale = 8f / 7f;

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
        /// 动作事件特效 (跟随角色或放在角色锚点处) 朝相机 (-Z) 前移的距离，保证画在触发者身前而不与其精灵同深度
        /// </summary>
        public const float ActionEffectOffset = 0.05f;

        /// <summary>
        /// 帧库像素 -> 世界单位
        /// </summary>
        public const float PixelToWorld = 1f / GameConfig.PIXELS_PER_UNIT;

        /// <summary>
        /// 逻辑坐标 (x, y 纵深, z 高度) -> 世界坐标 的线性变换矩阵 (用于 Gizmos 等需要整体变换的场合)
        /// </summary>
        public static readonly Matrix4x4 LogicToWorldMatrix = new Matrix4x4(
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(0f, 1f, 1f / DepthToScreenY, 0f),
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        /// <summary>
        /// 地面锚点：逻辑 (x, y 纵深) 在高度 0 处的世界坐标
        /// </summary>
        public static Vector3 GroundToWorld(float x, float depth) => new Vector3(x, depth, depth / DepthToScreenY);

        public static Vector3 ToWorld(float x, float depth, float height) => new Vector3(x, depth + height, depth / DepthToScreenY);

        public static Vector3 ToWorld(FPVector3 logic) => ToWorld(logic.x.AsFloat(), logic.y.AsFloat(), logic.z.AsFloat());

        /// <summary>
        /// 相机位置：画面中心对准横坐标 x、屏幕纵坐标 screenY (世界 Y)
        /// </summary>
        public static Vector3 CameraPosition(float x, float screenY) => new Vector3(x, screenY, -CameraDistance);
    }
}
