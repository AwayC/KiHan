using KiHan.Logic;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 逻辑实体基类
/// </summary>
public abstract class LogicEntity
{
<<<<<<< Updated upstream
    public Vector2 LogicPos; // 位置
    public float MoveSpeed = 5.0f; // 移动速度
    public const float LOGIC_TICK_TIME = 0.066f; // 逻辑帧间隔

    // 动画数据
    public AnimationFrameData IdleAnim;
    public AnimationFrameData RunAnim;

    public AnimationFrameData CurrentAnim; // 当前动画
    public int CurrentFrameIndex = 0; // 当前帧

    private int _tickCounter = 0; // 帧计数器
    public bool IsFacingLeft = false; // 朝向

    public void Tick(InputFrame input)
    {
        Debug.Log($"[Enity] player tick");

        if (input.JoyStickAngle == 255)
        {
            SwitchAnimation(IdleAnim);
        }
        else
        {
            ProcessMovement(input);
            SwitchAnimation(RunAnim);
        }

        UpdateAnimation();
    }

    private void ProcessMovement(InputFrame input)
    {
        // 映射 0-180 -> 0-360
        float degrees = input.JoyStickAngle * 2.0f;
        float radians = degrees * Mathf.Deg2Rad;
=======
    // --- 核心属性 ---
    public byte GameId; // 游戏内的唯一 ID (由服务器分配或动态生成)
    public Vector2 LogicPos;
    public float LogicHeight = 0; 
    
    public const float LOGIC_TICK_TIME = 0.066f;

    // --- 动画状态 ---
    public AnimationFrameData CurrentAnim;
    public int CurrentFrameIndex = 0; // Steps 索引
    protected int _tickCounter = 0;
    public bool IsFacingLeft = false;

    /// <summary>
    /// 每逻辑帧执行一次
    /// </summary>
    public virtual void Tick(InputFrame input)
    {
        UpdateAnimation();
    }

    public virtual void SwitchAnimation(AnimationFrameData newAnim)
    {
        if (newAnim == null || CurrentAnim == newAnim) return;
        CurrentAnim = newAnim;
        CurrentFrameIndex = 0;
        _tickCounter = 0;
    }
>>>>>>> Stashed changes

    protected virtual void UpdateAnimation()
    {
        if (CurrentAnim == null || CurrentAnim.Steps.Count == 0) return;

<<<<<<< Updated upstream
        float verticalMod = 0.7f;

        LogicPos.x += dx * MoveSpeed * LOGIC_TICK_TIME;
        LogicPos.y += dy * (MoveSpeed * verticalMod) * LOGIC_TICK_TIME;

        if (Mathf.Abs(dx) > 0.1f)
=======
        var step = CurrentAnim.Steps[CurrentFrameIndex];
        _tickCounter++;

        if (_tickCounter >= step.Duration)
>>>>>>> Stashed changes
        {
            _tickCounter = 0;
            if (CurrentFrameIndex < CurrentAnim.Steps.Count - 1)
            {
                CurrentFrameIndex++;
                ApplyRootMotion(CurrentAnim.Steps[CurrentFrameIndex].RootMotion);
            }
            else if (CurrentAnim.IsLoop) 
            {
                CurrentFrameIndex = 0;
            }
        }
    }
<<<<<<< Updated upstream
    public void SwitchAnimation(AnimationFrameData newAnim)
=======

    protected virtual void ApplyRootMotion(Vector2 motion)
>>>>>>> Stashed changes
    {
        if (motion == Vector2.zero) return;
        float direction = IsFacingLeft ? -1f : 1f;
        LogicPos.x += motion.x * direction;
        LogicHeight += motion.y; 
        if (LogicHeight < 0) LogicHeight = 0;
    }

<<<<<<< Updated upstream
        CurrentAnim = newAnim;
        CurrentFrameIndex = 0;
        _tickCounter = 0;
    }

    public void UpdateAnimation()
=======
    public List<LogicBox> GetCurrentHitBoxes()
    {
        if (CurrentAnim == null) return null;
        return CurrentAnim.GetHitBoxes(CurrentFrameIndex);
    }

    public List<LogicBox> GetCurrentHurtBoxes()
    {
        if (CurrentAnim == null) return null;
        return CurrentAnim.GetHurtBoxes(CurrentFrameIndex);
    }

    /// <summary>
    /// 检查本实体的攻击盒是否命中了目标实体的受击盒
    /// </summary>
    public bool CheckHit(LogicEntity target)
>>>>>>> Stashed changes
    {
        var myHits = GetCurrentHitBoxes();
        var targetHurts = target.GetCurrentHurtBoxes();

<<<<<<< Updated upstream
        var frameData = CurrentAnim.Frames[CurrentFrameIndex];

        _tickCounter++;

        if (_tickCounter >= frameData.Duration)
        {
            _tickCounter = 0;

            if (CurrentFrameIndex < CurrentAnim.Frames.Count - 1)
            {
                CurrentFrameIndex++;
                ApplyRootMotion(CurrentAnim.Frames[CurrentFrameIndex].RootMotion);
            }
            else
            {
                if (CurrentAnim.IsLoop)
                {
                    CurrentFrameIndex = 0;
                }
=======
        if (myHits == null || targetHurts == null) return false;

        foreach (var myBox in myHits)
        {
            foreach (var targetBox in targetHurts)
            {
                if (myBox.Intersects(LogicPos, 0, IsFacingLeft, 
                                     targetBox, target.LogicPos, 0, target.IsFacingLeft))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

/// <summary>
/// 角色实体 (受玩家输入控制)
/// </summary>
public class CharacterEntity : LogicEntity
{
    public float MoveSpeed = 5.0f;
    public AnimationFrameData IdleAnim;
    public AnimationFrameData RunAnim;

    public override void Tick(InputFrame input)
    {
        if (input != null)
        {
            if (input.JoyStickAngle == 255) 
            {
                SwitchAnimation(IdleAnim);
            }
            else
            {
                ProcessMovement(input);
                SwitchAnimation(RunAnim);
>>>>>>> Stashed changes
            }
        }

        base.Tick(input);
    }

<<<<<<< Updated upstream
    private void ApplyRootMotion(Vector2 motion)
=======
    private void ProcessMovement(InputFrame input)
>>>>>>> Stashed changes
    {
        float degrees = input.JoyStickAngle * 2.0f;
        float radians = degrees * Mathf.Deg2Rad;
        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);
        float verticalMod = 0.7f;

<<<<<<< Updated upstream
        float direction = IsFacingLeft ? -1f : 1f;
        LogicPos.x += motion.x * direction;
        LogicPos.y += motion.y;
=======
        LogicPos.x += dx * MoveSpeed * LOGIC_TICK_TIME;
        LogicPos.y += dy * (MoveSpeed * verticalMod) * LOGIC_TICK_TIME;

        if (Mathf.Abs(dx) > 0.1f) IsFacingLeft = dx < 0;
>>>>>>> Stashed changes
    }
}

<<<<<<< Updated upstream
    public Sprite GetCurrentSprite()
=======
/// <summary>
/// 技能派生实体 (如飞行道具、召唤物)
/// </summary>
public class SkillDerivedEntity : LogicEntity
{
    public Vector2 Velocity;
    public float LifeTime = 2.0f; // 存活时间 (秒)
    private float _timer = 0;

    public override void Tick(InputFrame input)
>>>>>>> Stashed changes
    {
        // 技能实体通常自主移动，不直接受玩家摇杆控制
        LogicPos += Velocity * LOGIC_TICK_TIME;
        
        _timer += LOGIC_TICK_TIME;
        if (_timer >= LifeTime)
        {
            // TODO: 通知 MapLogic 移除自己
        }

        base.Tick(input);
    }
}
