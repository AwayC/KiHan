using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;

// 定义通用状态索引
public static class CommonState
{
    public const sbyte Idle = 0;
    public const sbyte Run = 1;
    public const sbyte Attack = 2;
    public const sbyte Hurt = 4;
    public const sbyte Land = 6;
    public const sbyte skill = 7;
}

#region 通用基础状态类

public class CommonIdleState : StateBase
{
    public override sbyte StateType => CommonState.Idle;

    public override void Enter(CharacterEntity owner) => owner.SwitchAnimation("Idle");
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;
        if (input == null) return;
        if ((input.Buttons & ButtonMask.Attack) != 0) { 
            owner.RootSM.ChangeState(CommonState.Attack);
            return; 
        }
        //if ((input.Buttons & ButtonMask.Skill1) != 0) { 
        //    owner.ChangeState(CommonState.SkillA); return; 
        //}
        if (input.JoyStickAngle != 255) { 
            owner.RootSM.ChangeState(CommonState.Run); 
        }
    }
}

public class CommonRunState : StateBase
{
    public override sbyte StateType => CommonState.Run;

    public override void Enter(CharacterEntity owner) => owner.SwitchAnimation("Run");
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;
        if ((input.Buttons & ButtonMask.Attack) != 0) { owner.RootSM.ChangeState(CommonState.Attack); return; }
        if (input == null || input.JoyStickAngle == 255) { owner.RootSM.ChangeState(CommonState.Idle); return; }

        float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);
        owner.Pos.x += dx * owner.MoveSpeed * LogicEntity.LOGIC_TICK_TIME;
        owner.Pos.y += dy * (owner.MoveSpeed * 0.7f) * LogicEntity.LOGIC_TICK_TIME;
        if (Mathf.Abs(dx) > 0.1f) owner.IsFacingLeft = dx < 0;
    }
}

public class CommonHurtState : StateBase
{
    public override sbyte StateType => CommonState.Hurt;

    public override void Enter(CharacterEntity owner)
    {
        owner.Blood -= owner.LastHitData.Damage;
        owner.StunTime = owner.LastHitData.Damage;
        owner.StunTimer = 0;
        owner.SwitchAnimation($"Hurt_{(int)Random.Range(1, 4)}");
    }
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner)
    {
        bool isFinshed = owner.StunTimer >= owner.StunTime;

        if (isFinshed) 
        {
            owner.RootSM.ChangeState(CommonState.Idle);
        }
    }
}

#endregion
