using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;
using System;

// 定义通用状态索引
public static class CommonState
{
    public const sbyte Idle = 0;
    public const sbyte Run = 1;
    public const sbyte Attack = 2;
    public const sbyte Hurt = 4;
    public const sbyte Land = 6;
    public const sbyte Skill = 7;
}

#region 通用基础状态类
public class CommonIdleState : StateBase
{
    public override sbyte StateType => CommonState.Idle;
    private ButtonMask _lastButtons = ButtonMask.None;

    public override void Enter(CharacterEntity owner) 
    {
        owner.ForceLoop = true; // Idle 也是循环的
        owner.SwitchAnimation("Idle");
        // 变成 Idle 的时候速度归零
        owner.velocity = Vector2.zero;
        _lastButtons = owner.CurrInput?.Buttons ?? ButtonMask.None;
    }
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero; // 确保静止

        var input = owner.CurrInput;
        if (input == null) return;

        // 恢复为简单的状态检查：只要按着攻击键就进入攻击状态
        if ((input.Buttons & ButtonMask.Attack) != 0) {
            owner.RootSM.ChangeState(CommonState.Attack);
            return;
        }

        if (input.JoyStickAngle != 255) {
            owner.RootSM.ChangeState(CommonState.Run);
        }
    }
}

public class CommonRunState : StateBase
{
    public override sbyte StateType => CommonState.Run;

    public override void Enter(CharacterEntity owner) 
    {
        owner.ForceLoop = true; // 强制 Run 循环播放
        owner.SwitchAnimation("Run");

        // 立即更新一次朝向，处理短按情况
        var input = owner.CurrInput;
        if (input != null && input.JoyStickAngle != 255)
        {
            float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
            float dx = Mathf.Cos(radians);
            if (Mathf.Abs(dx) > 0.1f) owner.IsFacingLeft = dx < 0;
        }
    }
    public override void Exit(CharacterEntity owner) 
    {
        owner.ForceLoop = false;
        owner.velocity = Vector2.zero;

        Debug.Log("Run" + owner.pos);
    }
    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;
        if (input == null || input.JoyStickAngle == 255) { 
            owner.RootSM.ChangeState(CommonState.Idle); 
            return; 
        }
        
        if ((input.Buttons & ButtonMask.Attack) != 0) { 
            owner.RootSM.ChangeState(CommonState.Attack); 
            return; 
        }

        float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);
        
        owner.velocity = new Vector2(dx * owner.MoveSpeed, dy * owner.MoveSpeed * 0.6f);
        
        // 更新朝向
        if (Mathf.Abs(dx) > 0.1f) owner.IsFacingLeft = dx < 0;
    }
}

public class CommonHurtState : StateBase
{
    public override sbyte StateType => CommonState.Hurt;

    private int anim_idx = 1;

    public override void Enter(CharacterEntity owner)
    {
        // 简单播放受击动画
        owner.SwitchAnimation($"Hurt_{anim_idx}");
        anim_idx = anim_idx % 4 + 1;

        // 1. 初始化击退速度
        if (owner.LastHitData != null)
        {
            // 使用攻击包中指定的击退方向
            float pushDir = owner.LastHitData.PushDirX;
            
            // 如果攻击包没设方向（为0），则回退到根据位置计算
            if (pushDir == 0) pushDir = (owner.pos.x >= owner.LastHitData.Pos.x) ? 1.0f : -1f;
            
            owner.velocity = new Vector2(pushDir * owner.LastHitData.PushSpeed, 0);

            // 受击时强制转向攻击者
            owner.IsFacingLeft = (pushDir > 0);
        }
    }

    public override void Exit(CharacterEntity owner) 
    {
        owner.velocity = Vector2.zero;
    }

    public override void Update(CharacterEntity owner)
    {
        // 2. 击退速度衰减 (显著提高衰减系数 0.85 -> 0.65，使效果更短促有力)
        owner.velocity *= 0.5f;
        if (owner.velocity.magnitude < 0.1f) owner.velocity = Vector2.zero;

        // 3. 硬直计时器优先：只要还有时间，就强制停在受击状态
        if (owner.StunTimer > 0)
        {
            owner.StunTimer--;
            return;
        }

        owner.RootSM.ChangeState(CommonState.Idle);
    }
}

#endregion
