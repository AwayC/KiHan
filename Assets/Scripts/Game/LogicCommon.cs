using UnityEngine;
using System;
using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 同步特效图层信息
    /// </summary>
    [Serializable]
    public class EffectLayerInfo
    {
        public Sprite Sprite;
        public Vector2 Offset;
        public Color TintColor = Color.white;
        public int OrderOffset = 1; 
    }

    /// <summary>
    /// 受击动画表现类型
    /// </summary>
    public enum HitType
    {
        None, // 无效果
        Normal,      
        ToAir,
        
    }

    /// <summary>
    /// 攻击定义包
    /// </summary>
    [Serializable]
    public class HitData
    {
        public int Damage = 10; // 伤害数值
        public HitType HType = HitType.None;
        //public int HitStop = 8;   //  顿帧
        public int HitStun = 20;    // 僵直时间
        Vector2 Pos;
        int Height;
        LogicEntity Owner = null;
        CharacterEntity Player = null;

        public HitData(HitType htype = HitType.None)
        {
            HType = htype;
        }
    }

    [Serializable]
    public struct LogicBox // 通用 3D 逻辑判定盒
    {
        public Vector2 Center; 
        public Vector2 Size;   
        public float Side;     

        public LogicBox(Vector2 center, Vector2 size, float side)
        {
            Center = center;
            Size = size;
            Side = side;
        }

        public bool Intersects(Vector2 myPos, float myZ, bool myFacingLeft, LogicBox other, Vector2 otherPos, float otherZ, bool otherFacingLeft)
        {
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
