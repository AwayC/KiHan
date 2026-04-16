using UnityEngine;
using System;

namespace KiHan.Logic
{
    /// <summary>
    /// 同步特效图层信息 (用于刀光、虚影等)
    /// </summary>
    [Serializable]
    public class EffectLayerInfo
    {
        public Sprite Sprite;
        public Vector2 Offset;
        public Color TintColor = Color.white; // 默认设为纯白不透明
        public int OrderOffset = 1; 
    }

    [Serializable]
    public struct LogicBox // 通用 3D 逻辑判定盒
    {
        public Vector2 Center; // X, Y (相对于角色原点的中心)
        public Vector2 Size;   // Width, Height
        public float Side;     // 厚度 (轴宽，沿 Z 轴对称分布)

        public LogicBox(Vector2 center, Vector2 size, float side)
        {
            Center = center;
            Size = size;
            Side = side;
        }

        /// <summary>
        /// 碰撞检测：需要传入两个物体各自的世界坐标、Z 值以及是否朝左
        /// </summary>
        public bool Intersects(Vector2 myPos, float myZ, bool myFacingLeft, LogicBox other, Vector2 otherPos, float otherZ, bool otherFacingLeft)
        {
            // 如果朝左，Center.x 需要取反
            float myRealOffsetX = myFacingLeft ? -Center.x : Center.x;
            float otherRealOffsetX = otherFacingLeft ? -other.Center.x : other.Center.x;

            Vector2 myWorldCenter = new Vector2(myPos.x + myRealOffsetX, myPos.y + Center.y);
            Vector2 otherWorldCenter = new Vector2(otherPos.x + otherRealOffsetX, otherPos.y + other.Center.y);

            if (Mathf.Abs(myWorldCenter.x - otherWorldCenter.x) > (Size.x + other.Size.x) / 2) return false;
            if (Mathf.Abs(myWorldCenter.y - otherWorldCenter.y) > (Size.y + other.Size.y) / 2) return false;
            if (Mathf.Abs(myZ - otherZ) > (Side + other.Side) / 2) return false;
            
            return true;
        }
    }
}
