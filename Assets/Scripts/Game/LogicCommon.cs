using UnityEngine;
using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    [Serializable]
    public class EffectLayerInfo
    {
        public Sprite Sprite;

        /// <summary>
        /// 图层中心相对主精灵中心的偏移 (像素，y 向上)：图层节点挂在主精灵节点下，渲染位置 = 脚底 + 帧 Offset + 本 Offset
        /// </summary>
        public Vector2 Offset;
        public Color TintColor = Color.white;
        /// <summary>
        /// 图层前后次序：值越大越靠近相机 (表现层换算为朝相机的微小深度偏移)
        /// </summary>
        public int OrderOffset = 1;
    }

    /// <summary>
    /// 判定盒 (整数像素单位，0 浮点存储)
    /// 坐标原点为角色脚底中心，Center.x 为面朝右时的水平偏移，Center.y 为离脚底的高度偏移 (向上为正)
    /// 逻辑层通过 ToWorld() 转换为定点数世界 AABB，编辑器与表现层仍可直接按像素绘制
    /// </summary>
    [Serializable]
    public struct LogicBox
    {
        public Vector2Int Center;
        public Vector2Int Size;

        /// <summary>
        /// 纵深半轴 (像素，视觉单位，与原版判定盒 z 尺寸一致)：盒在纵深上覆盖 脚底纵深 ± Side，
        /// 两盒纵深相交即 |Δ纵深| ≤ 双方 Side 之和
        /// </summary>
        public int Side;

        /// <summary>
        /// 盒标签：匹配 ActionInfo.attackPhase / defensePhase 中的 tag，留空表示匹配任意开启的阶段
        /// </summary>
        public string Tag;

        public LogicBox(Vector2Int center, Vector2Int size, int side, string tag = "")
        {
            Center = center;
            Size = size;
            Side = side;
            Tag = tag;
        }

        public bool IsValid => Size.x > 0 && Size.y > 0;

        /// <summary>
        /// 转换为定点数世界 AABB
        /// </summary>
        /// <param name="rootPos">角色脚底逻辑坐标 (x: 横向, y: 纵深, z: 高度)</param>
        /// <param name="facingLeft">是否面朝左 (水平偏移镜像)</param>
        public FPBox ToWorld(FPVector3 rootPos, bool facingLeft)
        {
            FP cx = rootPos.x + GameConfig.PixelToUnit(facingLeft ? -Center.x : Center.x);
            FP cz = rootPos.z + GameConfig.PixelToUnit(Center.y);
            FP halfW = GameConfig.PixelToUnit(Size.x) * FP.Half;
            FP halfH = GameConfig.PixelToUnit(Size.y) * FP.Half;
            FP halfD = GameConfig.PixelToUnit(Side);

            return new FPBox(
                new FPVector3(cx - halfW, rootPos.y - halfD, cz - halfH),
                new FPVector3(cx + halfW, rootPos.y + halfD, cz + halfH)
            );
        }
    }
}
