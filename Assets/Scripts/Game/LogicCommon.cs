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
