using KiHan.Logic;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class LogicEntity
{
    public int EntityId;     // 实体唯一 ID
    public Vector2 pos;      // 水平坐标
    public int height;       // 高度
    public Vector2 velocity; // 水平速度
    public int h_vel;        // 垂直速度
    public int Gravity = 10;  // 重力加速度 (新增：允许动态修改)
    public int owner;        // 所属玩家 ID

    public Vector2 CurrentVelocity; // 当前水平真实速度 (units/s)，提供给 View 推算
    public float CurrentHVelocity;  // 当前垂直真实速度 (units/s)，提供给 View 推算

    // --- 图像与动画 (纯逻辑层) ---
    public AnimationFrameData CurrAnim; 
    public int CurrentFrameIndex;
    public bool IsFacingLeft = true;
    public int LogicalTickCounter; // 逻辑层专属计时器
    public bool ForceLoop = false; // 逻辑层控制的强制循环
    public bool ForceNotLoop = false; // 新增：强制不循环（防止美术误勾选）
    public int AnimVersion = 0;    // 动画版本号，用于通知表现层重置计时器

    public static float LOGIC_TICK_TIME => GameConfig.LOGIC_TICK_TIME;

    protected Dictionary<string, AnimationFrameData> _animDict = new Dictionary<string, AnimationFrameData>();
    protected HashSet<int> _hitRegistry = new HashSet<int>();
    protected HitData _currHitData = null;

    public virtual void Tick()
    {
        // 优先更新 tick counter
        UpdateTickCounter();
        ProcessPhysics();
    }

    public HitData GetHitData()
    {
        return _currHitData;
    }
    public abstract void LoadRes(string basePath);

    public virtual void HitCallback() { }

    public bool CanHit(LogicEntity target)
    {
        return _currHitData != null && !_hitRegistry.Contains(target.EntityId);
    }

    public void RegisterHit(LogicEntity target)
    {
        _hitRegistry.Add(target.EntityId);
    }

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

    public bool IsAnimEnd()
    {
        if (CurrAnim == null) return true;
        var steps = CurrAnim.Steps;
        int frameIdx = Mathf.Clamp(CurrentFrameIndex, 0, steps.Count - 1);

        if (frameIdx >= steps.Count - 1)
        {
            var lastStep = steps[frameIdx];
            if (LogicalTickCounter >= lastStep.Duration)
                return true;
        }
        return false;
    }

    public void SetHitData(HitData hitData)
    {
        _hitRegistry.Clear();
        _currHitData = hitData;
    }

    protected void ClearHitData()
    {
        _hitRegistry.Clear();
        _currHitData = null;
    }

    protected virtual void ProcessPhysics()
    {
        Vector2 finalVel = velocity;
        if (CurrAnim != null && CurrentFrameIndex < CurrAnim.Steps.Count)
        {
            var step = CurrAnim.Steps[CurrentFrameIndex];
            
            // X轴：视为该帧的总位移 (Displacement)
            if (step.RootMotion.x != 0 && step.Duration > 0)
            {
                float p2u = 0.01f;
                float stepMoveX = (step.RootMotion.x * p2u) * GameConfig.RENDER_LOGIC_RATIO;
                if (IsFacingLeft) stepMoveX = -stepMoveX;
                finalVel.x += stepMoveX / LOGIC_TICK_TIME;
            }

            // Y轴：直接视为垂直速度 (Vertical Velocity)
            if (step.RootMotion.y != 0)
            {
                h_vel = (int)step.RootMotion.y;
            }
        }

        // 1. 处理重力与垂直逻辑
        if (height > 0 || h_vel != 0)
        {
            h_vel -= Gravity; // 使用动态重力
            height += h_vel;
            if (height <= 0)
            {
                height = 0;
                h_vel = 0;
            }
        }

        // 2. 统一处理地面水平位移
        pos += finalVel * LOGIC_TICK_TIME;

        // 3. 地图边界限制
        if (MapManager.Instance != null && MapManager.Instance.CurrentMapLogic != null)
        {
            var map = MapManager.Instance.CurrentMapLogic;
            pos.x = Mathf.Clamp(pos.x, map.MinX, map.MaxX);
            pos.y = Mathf.Clamp(pos.y, map.MinY, map.MaxY);
        }
    }

    protected virtual void UpdateTickCounter()
    {
        if (CurrAnim == null || CurrAnim.Steps.Count == 0) return;

        // 逻辑层依然使用 RENDER_LOGIC_RATIO 来计算经过的“表现层帧数时间”
        LogicalTickCounter += GameConfig.RENDER_LOGIC_RATIO;

        // 使用 while 循环来增加鲁棒性：如果经过的时间足以跨越多个关键帧，则连续推进
        while (true)
        {
            var step = CurrAnim.Steps[CurrentFrameIndex];
            
            // 防止美术配置失误导致 Duration = 0 引发死循环，保底给 1
            int duration = step.Duration > 0 ? step.Duration : 1;

            if (LogicalTickCounter >= duration)
            {
                if (CurrentFrameIndex < CurrAnim.Steps.Count - 1)
                {
                    LogicalTickCounter -= duration;
                    CurrentFrameIndex++;
                }
                else if ((CurrAnim.IsLoop && !ForceNotLoop) || ForceLoop)
                {
                    LogicalTickCounter -= duration;
                    CurrentFrameIndex = 0;
                }
                else
                {
                    // 如果是最后一帧且不循环，跳出循环允许 LogicalTickCounter 继续增加
                    break;
                }
            }
            else
            {
                // 当前积累的时间不足以跨越当前帧，结束更新
                break;
            }
        }
    }

    public void SwitchAnimation(string animName, bool forceReset = false)
    {
        if (_animDict.TryGetValue(animName, out var data))
        {
            if (CurrAnim == data && !forceReset) return;
            CurrAnim = data;
            CurrentFrameIndex = 0;
            LogicalTickCounter = 0;
            AnimVersion++;
            ClearHitData(); 
        }
    }

    public AnimationFrameData GetAnim(string animName)
    {
        _animDict.TryGetValue(animName, out var data);
        return data;
    }
}
