using KiHan.Logic;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态接口
/// </summary>
public interface IEntityState
{
    sbyte StateType { get; } 
    void Enter(CharacterEntity entity);
    void Tick(CharacterEntity entity, InputFrame input);
    void Exit(CharacterEntity entity);
}

/// <summary>
/// 逻辑实体基类
/// </summary>
public abstract class LogicEntity
{
    public byte GameId; 
    public Vector2 LogicPos;
    public float LogicHeight = 0; 
    
    public const float LOGIC_TICK_TIME = 0.066f;

    public AnimationFrameData CurrentAnim;
    public int CurrentFrameIndex = 0; 
    protected int _tickCounter = 0;
    public bool IsFacingLeft = false;

    public int GetTickCounter() => _tickCounter;

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

    protected virtual void UpdateAnimation()
    {
        if (CurrentAnim == null || CurrentAnim.Steps.Count == 0) return;

        var step = CurrentAnim.Steps[CurrentFrameIndex];
        _tickCounter++;

        if (_tickCounter >= step.Duration)
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

    protected virtual void ApplyRootMotion(Vector2 motion)
    {
        if (motion == Vector2.zero) return;
        float direction = IsFacingLeft ? -1f : 1f;
        LogicPos.x += motion.x * direction;
        LogicHeight += motion.y; 
        if (LogicHeight < 0) LogicHeight = 0;
    }

    public List<LogicBox> GetCurrentHitBoxes() => CurrentAnim?.GetHitBoxes(CurrentFrameIndex);
    public List<LogicBox> GetCurrentHurtBoxes() => CurrentAnim?.GetHurtBoxes(CurrentFrameIndex);

    public bool CheckHit(LogicEntity target)
    {
        var myHits = GetCurrentHitBoxes();
        var targetHurts = target.GetCurrentHurtBoxes();
        if (myHits == null || targetHurts == null) return false;

        foreach (var myBox in myHits)
            foreach (var targetBox in targetHurts)
                if (myBox.Intersects(LogicPos, 0, IsFacingLeft, targetBox, target.LogicPos, 0, target.IsFacingLeft))
                    return true;
        return false;
    }
}

/// <summary>
/// 角色实体 (引入 FSM)
/// </summary>
public class CharacterEntity : LogicEntity
{
    public float MoveSpeed = 5.0f;
    
    public IEntityState CurrentState { get; private set; }
    protected Dictionary<sbyte, IEntityState> _states = new Dictionary<sbyte, IEntityState>();

    public override void Tick(InputFrame input)
    {
        // 1. 状态机驱动
        CurrentState?.Tick(this, input);
        // 2. 动画驱动
        base.Tick(input);
    }

    public void ChangeState(sbyte stateType)
    {
        if (CurrentState != null && CurrentState.StateType == stateType) return;
        if (!_states.TryGetValue(stateType, out var newState)) return;

        CurrentState?.Exit(this);
        CurrentState = newState;
        CurrentState.Enter(this);
    }

    public void AddState(IEntityState state)
    {
        _states[state.StateType] = state;
    }
}

/// <summary>
/// 技能派生实体
/// </summary>
public class SkillDerivedEntity : LogicEntity
{
    public Vector2 Velocity;
    public float LifeTime = 2.0f; 
    private float _timer = 0;

    public override void Tick(InputFrame input)
    {
        LogicPos += Velocity * LOGIC_TICK_TIME;
        _timer += LOGIC_TICK_TIME;
        base.Tick(input);
    }
}
