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

    public override void Enter(CharacterEntity owner) 
    {
        owner.ForceLoop = true; // Idle 也是循环的
        owner.SwitchAnimation("Idle");
        // 变成 Idle 的时候速度归零
        owner.velocity = Vector2.zero;
    }
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero; // 确保静止

        var input = owner.CurrInput;
        if (input == null) return;

        if ((input.Buttons & ButtonMask.Attack) != 0) {
            owner.RootSM.ChangeState(CommonState.Attack);
            return;
        }

        if (input.JoyStickAngle != 255) {
            owner.RootSM.ChangeState(CommonState.Run);
        }
    }}

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

    public override void Enter(CharacterEntity owner)
    {
        // 简单播放受击动画
        owner.SwitchAnimation("Hurt_1");
    }

    public override void Exit(CharacterEntity owner) { }

    public override void Update(CharacterEntity owner)
    {
        // 这里可以根据受击包的硬直时间来判断退出
        // 目前简化为动画播放完毕退出
        if (owner.TickCounter >= (owner.CurrAnim?.Steps[owner.CurrentFrameIndex].Duration ?? 1) - 1 
            && owner.CurrentFrameIndex >= (owner.CurrAnim?.Steps.Count ?? 1) - 1)
        {
            owner.RootSM.ChangeState(CommonState.Idle);
        }
    }
}

#endregion
