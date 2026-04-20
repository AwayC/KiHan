using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;

// 鸣人特有的状态放在这里
#region 战斗状态 (Attack / Skill)

public class NarutoAttackState : EntityState
{
    public override sbyte StateType => CommonState.Idle;
    public List<AnimationFrameData> Anims;
    
    private int _comboIndex = 0;
    private bool _requestNextCombo = false;

    public override void Enter(CharacterEntity owner)
    {
        _comboIndex = 0;
        _requestNextCombo = false;
        owner.SwitchAnimation(Anims[_comboIndex]);
    }

    public override void Update(CharacterEntity owner, InputFrame input)
    {
        if (input != null && (input.Buttons & ButtonMask.Attack) != 0)
        {
            _requestNextCombo = true;
        }

        if (owner.CurrentFrameIndex >= owner.CurrentAnim.Steps.Count - 1)
        {
            var step = owner.CurrentAnim.Steps[owner.CurrentFrameIndex];
            if (owner.GetTickCounter() >= step.Duration - 1)
            {
                if (_requestNextCombo && _comboIndex < Anims.Count - 1)
                {
                    _comboIndex++;
                    _requestNextCombo = false;
                    owner.SwitchAnimation(Anims[_comboIndex]);
                }
                else
                {
                    owner.ChangeState(CommonState.Idle);
                }
            }
        }
    }

    public override void Exit(CharacterEntity owner) { }
}

public class NarutoSkillAState : EntityState
{
    public override sbyte StateType => CommonState.Idle;
    public List<AnimationFrameData> Anims;
    private int _subState = 0;

    public override void Enter(CharacterEntity owner)
    {
        _subState = 0;
        owner.SwitchAnimation(Anims[0]);
    }

    public override void Update(CharacterEntity owner, InputFrame input)
    {
        if (owner.CurrentFrameIndex >= owner.CurrentAnim.Steps.Count - 1)
        {
            var step = owner.CurrentAnim.Steps[owner.CurrentFrameIndex];
            if (owner.GetTickCounter() >= step.Duration - 1)
            {
                if (_subState < Anims.Count - 1)
                {
                    _subState++;
                    owner.SwitchAnimation(Anims[_subState]);
                }
                else
                {
                    owner.ChangeState(CommonState.Idle);
                }
            }
        }
    }

    public override void Exit(CharacterEntity owner) { }
}

#endregion
