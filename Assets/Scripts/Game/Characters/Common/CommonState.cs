using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;

// 定义通用状态索引
public static class CommonState
{
    public const sbyte Idle = 0;
    public const sbyte Run = 1;
    public const sbyte Hurt_1 = 2;
    public const sbyte Hurt_2 = 3;
    public const sbyte Hurt_3 = 4;
    public const sbyte Hurt_4 = 5;
    public const sbyte Hurt_cmd = 6;
    public const sbyte Hurt_fall = 7;
    public const sbyte Hurt_inair = 8;
    public const sbyte Hurt_land = 9;
    public const sbyte Hurt_toair = 10;
    public const sbyte Land = 11;
}

#region 通用基础状态类

public class CommonIdleState : EntityState
{
    public override sbyte StateType => CommonState.Idle;
    public AnimationFrameData Anim;

    public override void Enter(CharacterEntity owner) => owner.SwitchAnimation(Anim);
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner, InputFrame input)
    {
        if (input == null) return;
        //if ((input.Buttons & ButtonMask.Attack) != 0) { owner.ChangeState(CommonState.Attack); return; }
        //if ((input.Buttons & ButtonMask.Skill1) != 0) { owner.ChangeState(CommonState.SkillA); return; }
        if (input.JoyStickAngle != 255) owner.ChangeState(CommonState.Run);
    }
}

public class CommonRunState : EntityState
{
    public override sbyte StateType => CommonState.Run;
    public AnimationFrameData Anim;

    public override void Enter(CharacterEntity owner) => owner.SwitchAnimation(Anim);
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner, InputFrame input)
    {
        //if (input == null || input.JoyStickAngle == 255) { owner.ChangeState(CommonState.Idle); return; }
        //if ((input.Buttons & ButtonMask.Attack) != 0) { owner.ChangeState(CommonState.Attack); return; }

        float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);
        owner.LogicPos.x += dx * owner.MoveSpeed * LogicEntity.LOGIC_TICK_TIME;
        owner.LogicPos.y += dy * (owner.MoveSpeed * 0.7f) * LogicEntity.LOGIC_TICK_TIME;
        if (Mathf.Abs(dx) > 0.1f) owner.IsFacingLeft = dx < 0;
    }
}

public class CommonHurtState : EntityState
{
    public override sbyte StateType => CommonState.Idle;
    public List<AnimationFrameData> Anims;

    public override void Enter(CharacterEntity owner)
    {
        if (Anims != null && Anims.Count > 0) owner.SwitchAnimation(Anims[0]);
    }
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner, InputFrame input)
    {
        if (owner.CurrentFrameIndex >= owner.CurrentAnim.Steps.Count - 1)
        {
            var step = owner.CurrentAnim.Steps[owner.CurrentFrameIndex];
            if (owner.GetTickCounter() >= step.Duration - 1) owner.ChangeState(CommonState.Idle);
        }
    }
}

public class CommonLandState : EntityState
{
    public override sbyte StateType => CommonState.Land;
    public AnimationFrameData Anim;
    public override void Enter(CharacterEntity owner) => owner.SwitchAnimation(Anim);
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner, InputFrame input)
    {
        if (owner.CurrentFrameIndex >= owner.CurrentAnim.Steps.Count - 1)
        {
            var step = owner.CurrentAnim.Steps[owner.CurrentFrameIndex];
            if (owner.GetTickCounter() >= step.Duration - 1) owner.ChangeState(CommonState.Idle);
        }
    }
}

#endregion
