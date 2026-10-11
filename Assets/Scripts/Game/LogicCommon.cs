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

        // --- 特效配置 ---
        public string HitEffectName = "";  // 打击特效名 (Resources/Effects 下的预制体)
        public Vector2 HitEffectOffset;    // 特效生成的相对偏移位置
        
        // 攻击者的信息，供受击方参考
        public Vector2 Pos;
        public int Height;
        public LogicEntity Owner = null;
        public LogicEntity Player = null;

        public Action<LogicEntity> HitCallBack = null;  // 击中回调

        public HitData(HitType htype = HitType.None)
        {
            HType = htype;
        }

        public void CallHitOwner()
        {
            if (Owner != null)
            {
                HitCallBack?.Invoke(Owner);
            }
        }
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
        /// 纵深厚度 (像素)
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
            FP halfD = GameConfig.PixelToUnit(Side) * FP.Half;

            return new FPBox(
                new FPVector3(cx - halfW, rootPos.y - halfD, cz - halfH),
                new FPVector3(cx + halfW, rootPos.y + halfD, cz + halfH)
            );
        }

        /// <summary>
        /// 旧内核 (LogicEntity) 兼容接口，M8 迁移完成后随 LogicEntity 一并删除
        /// </summary>
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
            if (Mathf.Abs(myPos.y - otherPos.y) > (mySide + otherSide) / 2) return false;

            // --- C. 垂直高度 (Z轴) 判定 ---
            float myWorldZ = myZ * p2u + myRealOffsetY;
            float otherWorldZ = otherZ * p2u + otherRealOffsetY;
            if (Mathf.Abs(myWorldZ - otherWorldZ) > (mySizeY + otherSizeY) / 2) return false;

            return true;
        }
    }
}
