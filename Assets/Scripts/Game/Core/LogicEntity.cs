using KiHan.Logic;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class LogicEntity
{
    public Vector2 pos;      // 水平坐标
    public int height;       // 高度
    public Vector2 velocity; // 水平速度
    public int h_vel;        // 垂直速度
    public int owner;        // 所属玩家 ID

    // --- 图像与动画 ---
    public AnimationFrameData CurrAnim; 
    public int CurrentFrameIndex;
    public bool IsFacingLeft = true;
    public int TickCounter;
    public bool ForceLoop = false; // 逻辑层控制的强制循环

    public const float LOGIC_TICK_TIME = 0.066f;

    protected Dictionary<string, AnimationFrameData> _animDict = new Dictionary<string, AnimationFrameData>();

    public virtual void Tick()
    {
        // 顺序没有问题（不要修改）
        ProcessPhysics();
        UpdateAnim();
    }

    public abstract HitData GetHitData();
    public abstract void LoadRes(string basePath);

    public virtual void HitExit() { }

    public bool CheckHit(LogicEntity target)
    {
        if (target == this) return false;
        var myHits = CurrAnim?.GetHitBoxes(CurrentFrameIndex);
        var targetHurts = target.CurrAnim?.GetHurtBoxes(target.CurrentFrameIndex);

        if (myHits == null || targetHurts == null) return false;

        foreach (var myBox in myHits)
        {
            foreach (var targetBox in targetHurts)
            {
                if (myBox.Intersects(pos, height, IsFacingLeft, targetBox, target.pos, target.height, target.IsFacingLeft))
                    return true;
            }
        }
        return false;
    }

    protected virtual void ProcessPhysics()
    {
        // 1. 处理重力
        if (height > 0 || h_vel != 0)
        {
            h_vel -= 5; 
            height += h_vel;
            if (height <= 0)
            {
                height = 0;
                h_vel = 0;
            }
        }

        // 2. 统一处理地面水平位移 (所有位移都在这里更新)
        pos += velocity * LOGIC_TICK_TIME;

        // 3. 地图边界限制
        if (MapManager.Instance != null && MapManager.Instance.CurrentMapLogic != null)
        {
            var map = MapManager.Instance.CurrentMapLogic;
            pos.x = Mathf.Clamp(pos.x, map.MinX, map.MaxX);
            pos.y = Mathf.Clamp(pos.y, map.MinY, map.MaxY);
        }
    }

    protected virtual void UpdateAnim()
    {
        if (CurrAnim == null || CurrAnim.Steps.Count == 0) return;

        var step = CurrAnim.Steps[CurrentFrameIndex];

        TickCounter++;

        if (TickCounter >= step.Duration)
        {
            TickCounter = 0;
            if (CurrentFrameIndex < CurrAnim.Steps.Count - 1)
            {
                CurrentFrameIndex++;
            }
            else if (CurrAnim.IsLoop || ForceLoop)
            {
                CurrentFrameIndex = 0;
            }
        }
    }

    public void SwitchAnimation(string animName)
    {
        if (_animDict.TryGetValue(animName, out var data))
        {
            if (CurrAnim == data) return;
            CurrAnim = data;
            CurrentFrameIndex = 0;
            TickCounter = 0;
        }
    }
}
