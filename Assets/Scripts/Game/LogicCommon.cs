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
        public float PushSpeed = 2.0f;     // 水平击退速度
        public float PushDirX = 0;         // 击退方向 (-1 或 1)
        public int PushSpeedY = 0;         // 垂直击飞速度 (新增)
        public float PushSpeedAir = 0;     // 空中击飞速度（新增）
        public bool IsHeavyHit = false;    // 是否重击（用于触发连震等表现）
        public bool IsSkill = false;       // 是否是技能
        
        // 攻击者的信息，供受击方参考
        public Vector2 Pos;
        public int Height;
        public LogicEntity Owner = null;
        public CharacterEntity Player = null;

        public HitData(HitType htype = HitType.None)
        {
            HType = htype;
        }

        public void CallHitOwner()
        {
            Owner.HitCallback();
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

            // 1. 转换判定参数为 Unity 坐标系
            float myRealOffsetX = (myFacingLeft ? -Center.x : Center.x) * p2u;
            float myRealOffsetY = Center.y * p2u;
            float mySizeX = Size.x * p2u;
            float mySizeY = Size.y * p2u;
            float mySide = Side * p2u;

            float otherRealOffsetX = (otherFacingLeft ? -other.Center.x : other.Center.x) * p2u;
            float otherRealOffsetY = other.Center.y * p2u;
            float otherSizeX = other.Size.x * p2u;
            float otherSizeY = other.Size.y * p2u;
            float otherSide = other.Side * p2u;

            // --- A. 水平 (X轴) 判定 ---
            float myWorldX = myPos.x + myRealOffsetX;
            float otherWorldX = otherPos.x + otherRealOffsetX;
            if (Mathf.Abs(myWorldX - otherWorldX) > (mySizeX + otherSizeX) / 2) return false;

            // --- B. 地图深度 (Y轴) 判定 ---
            // 注意：myPos.y 是角色在地图上的深度坐标，对应 Side 厚度
            if (Mathf.Abs(myPos.y - otherPos.y) > (mySide + otherSide) / 2) return false;

            // --- C. 垂直高度 (Z轴) 判定 ---
            // 注意：height 是跳跃高度，Center.y 是判定盒在立绘上的高度偏移，对应 Size.y 判定盒高度
            float myWorldZ = myZ * p2u + myRealOffsetY;
            float otherWorldZ = otherZ * p2u + otherRealOffsetY;
            if (Mathf.Abs(myWorldZ - otherWorldZ) > (mySizeY + otherSizeY) / 2) return false;
            
            return true;
        }
    }
}
