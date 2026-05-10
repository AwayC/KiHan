using UnityEngine;
using System;
using System.Collections.Generic;

namespace KiHan.Logic
{
    [Serializable]
    public class EffectLayerInfo
    {
        public Sprite Sprite;
        public Vector2 Offset;
        public Color TintColor = Color.white;
        public int OrderOffset = 1; 
    }

    public enum HitType
    {
        None, 
        Normal,      
        ToAir,
    }

    [Serializable]
    public class HitData
    {
        public int Damage = 10;
        public HitType HType = HitType.None;
        public int HitStun = 20;    
        public float PushSpeed = 2.0f;     // 击退速度
        public float PushDirX = 0;         // 击退方向 (-1 或 1)
        
        // 攻击者的信息，供受击方参考
        public Vector2 Pos;
        public int Height;
        public LogicEntity Owner = null;
        public CharacterEntity Player = null;

        public HitData(HitType htype = HitType.None)
        {
            HType = htype;
        }
    }

    [Serializable]
    public struct LogicBox
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
            float p2u = 0.01f; // 像素转逻辑单位

            // 转换自身坐标和尺寸
            float myRealOffsetX = (myFacingLeft ? -Center.x : Center.x) * p2u;
            float myRealOffsetY = Center.y * p2u;
            float mySizeX = Size.x * p2u;
            float mySizeY = Size.y * p2u;
            float mySide = Side * p2u;

            // 转换对方坐标和尺寸
            float otherRealOffsetX = (otherFacingLeft ? -other.Center.x : other.Center.x) * p2u;
            float otherRealOffsetY = other.Center.y * p2u;
            float otherSizeX = other.Size.x * p2u;
            float otherSizeY = other.Size.y * p2u;
            float otherSide = other.Side * p2u;

            Vector2 myWorldCenter = new Vector2(myPos.x + myRealOffsetX, myPos.y + myRealOffsetY);
            Vector2 otherWorldCenter = new Vector2(otherPos.x + otherRealOffsetX, otherPos.y + otherRealOffsetY);

            if (Mathf.Abs(myWorldCenter.x - otherWorldCenter.x) > (mySizeX + otherSizeX) / 2) return false;
            if (Mathf.Abs(myWorldCenter.y - otherWorldCenter.y) > (mySizeY + otherSizeY) / 2) return false;
            if (Mathf.Abs(myZ - otherZ) > (mySide + otherSide) / 2) return false;
            
            return true;
        }
    }
}
