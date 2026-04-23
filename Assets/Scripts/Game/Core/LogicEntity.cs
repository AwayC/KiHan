using KiHan.Logic;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 逻辑实体基类
/// </summary>
public abstract class LogicEntity
{
    public int GameId;
    public Vector2 Pos;
    public float Height = 0;
    public Vector2 Velocity; // 水平速度
    public float HVelocity; // 垂直速度
    public byte owner; // 所属玩家
    
    public const float LOGIC_TICK_TIME = 0.066f;
    public const float GRAVITY = 0.5f;

    public AnimationFrameData CurrentAnim;
    public int CurrentFrameIndex = 0; 
    protected int _tickCounter = 0;
    public bool IsFacingLeft = false;

    Dictionary<string, AnimationFrameData> _anim;

    public virtual void Tick()
    {
        UpdateAnimation();
    }

    private void ProcessPhysics()
    {
        
    }

    protected virtual void UpdateAnimation()
    {
        if (CurrentAnim == null || CurrentAnim.Steps.Count == 0) return;
        
        var step = CurrentAnim.Steps[CurrentFrameIndex];
        if (step.Duration < 0) return;
        _tickCounter++;

        if (_tickCounter >= step.Duration)
        {
            _tickCounter = 0;
            if (CurrentFrameIndex < CurrentAnim.Steps.Count - 1) CurrentFrameIndex++;
            else if (CurrentAnim.IsLoop) CurrentFrameIndex = 0;
        }
    }

    public List<LogicBox> GetCurrentHitBoxes() => CurrentAnim?.GetHitBoxes(CurrentFrameIndex);
    public List<LogicBox> GetCurrentHurtBoxes() => CurrentAnim?.GetHurtBoxes(CurrentFrameIndex);
    public virtual HitData GetHitData() 
    {
        return new HitData();
    }

    public int GetTickCounter() => _tickCounter;

    public bool CheckHit(LogicEntity target)
    {
        var myHits = GetCurrentHitBoxes();
        var targetHurts = target.GetCurrentHurtBoxes();
        if (myHits == null || targetHurts == null) return false;
        foreach (var myBox in myHits)
            foreach (var targetBox in targetHurts)
                if (myBox.Intersects(Pos, Height, IsFacingLeft, targetBox, target.Pos, Height, target.IsFacingLeft))
                    return true;
        return false;
    }

    public abstract void LoadRes(string basePath);

    public void SwitchAnimation(string anim)
    {
        // todo
    }
}
