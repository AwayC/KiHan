using KiHan.Logic;
using System.Collections.Generic;
using UnityEngine;

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
    public int GetTickCounter() => _tickCounter;

    /// <summary>
    /// 被攻击时的逻辑入口
    /// </summary>
    public virtual void TakeDamage(int damageType)
    {
        // 核心逻辑：一旦受击，强制切换到受击状态
        // 这里的 damageType 可以用来区分 轻击、重击、击飞等
        //ChangeState(CommonState.Hurt);
    }

    // 状态机相关方法移动到基类以方便统一调用
    public abstract void ChangeState(sbyte type);

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
