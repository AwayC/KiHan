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
    public int owner;        // 所属玩家 ID

    // --- 图像与动画 ---
    public AnimationFrameData CurrAnim; 
    public int CurrentFrameIndex;
    public bool IsFacingLeft = true;
    public int TickCounter;
    public bool ForceLoop = false; // 逻辑层控制的强制循环
    public bool FreezeAnimFrame = false; // 冻结当前帧（用于等待落地等特殊逻辑）
    public int AnimVersion;        // 动画版本号，用于通知表现层重置计时器

    public static float LOGIC_TICK_TIME => GameConfig.LOGIC_TICK_TIME;

    protected Dictionary<string, AnimationFrameData> _animDict = new Dictionary<string, AnimationFrameData>();
    protected HashSet<int> _hitRegistry = new HashSet<int>(); // 记录当前动画段落已命中的目标 ID
    protected bool _hadHitboxLastTick = false;

    public virtual void Tick()
    {
        // 顺序没有问题（不要修改）
        ProcessPhysics();
        // UpdateAnim() 移出 Tick，由 GameApp 统一在碰撞检测后调用，防止跳帧
    }

    public abstract HitData GetHitData();
    public abstract void LoadRes(string basePath);

    public virtual void HitExit() { }

    public void AdvanceAnimation()
    {
        UpdateAnim();
    }

    /// <summary>
    /// 检查是否可以命中目标（防止同一段动作重复命中）
    /// </summary>
    public bool CanHit(LogicEntity target)
    {
        return !_hitRegistry.Contains(target.EntityId);
    }

    /// <summary>
    /// 记录已命中的目标
    /// </summary>
    public void RegisterHit(LogicEntity target)
    {
        _hitRegistry.Add(target.EntityId);
    }

    public void ClearHitRegistry()
    {
        _hitRegistry.Clear();
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

    protected virtual void ProcessPhysics()
    {
        // 0. 叠加动画位移 (Root Motion) 预处理
        Vector2 finalVel = velocity;
        if (CurrAnim != null && CurrentFrameIndex < CurrAnim.Steps.Count)
        {
            var step = CurrAnim.Steps[CurrentFrameIndex];
            
            // X轴：视为该帧的总位移 (Displacement)
            if (step.RootMotion.x != 0 && step.Duration > 0)
            {
                float p2u = 0.01f;
                float stepMoveX = (step.RootMotion.x * p2u / step.Duration) * GameConfig.RENDER_LOGIC_RATIO;
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
            h_vel -= 5; // 重力加速度
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

    protected virtual void UpdateAnim()
    {
        if (CurrAnim == null || CurrAnim.Steps.Count == 0) return;

        // 如果被冻结（例如等待落地），则不推进计时器和帧索引
        if (FreezeAnimFrame) return;

        // --- 多段打击支持核心逻辑 ---
        var hits = CurrAnim.GetHitBoxes(CurrentFrameIndex);
        bool hasHits = hits != null && hits.Count > 0;
        // 如果这一帧新出现了攻击盒（上一帧没有），或者切换了帧，我们检查是否需要重置
        // 这里采用：只要这一帧有攻击盒且上一帧没有，就视为“新的一段”攻击
        if (hasHits && !_hadHitboxLastTick)
        {
            ClearHitRegistry();
        }
        _hadHitboxLastTick = hasHits;

        var step = CurrAnim.Steps[CurrentFrameIndex];
        TickCounter += GameConfig.RENDER_LOGIC_RATIO;

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
                ClearHitRegistry(); 
                _hadHitboxLastTick = false;
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
            AnimVersion++;
            ClearHitRegistry(); 
            _hadHitboxLastTick = false;
        }
    }
}
