using KiHan.Logic;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NarutoStateSkillB : StateBase
{
    private bool _isLand = false;
    private int _comIdx = 1;
    private bool _isCom1Hitted = false;
    private bool _hasCreatedFx = false;
    private bool _hasSetHit1 = false;
    public override sbyte StateType => NarutoState.SkillB;

    public override void Enter(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;

        _isLand = false;
        _comIdx = 1;
        _isCom1Hitted = false;
        _hasCreatedFx = false;
        _hasSetHit1 = false;
        owner.ForceLoop = false;
        owner.SwitchAnimation("Skill_B_1");
        owner.armorLevel = ArmorLevel.skill;

        //EventManager.Instance.Emit("PlayEffect", effect);
        //EventManager.Instance.Emit("PlayEffect", effectFeng);
    }

    public override void Update(CharacterEntity owner)
    {
        int lastFrame = (owner.CurrAnim != null) ? owner.CurrAnim.Steps.Count - 1 : 9;

        if(_comIdx == 1)
        {
            UpdateCom1(owner);
        } else
        {
            UpdateCom2(owner);
        }

        
    }

    private void UpdateCom1(CharacterEntity owner)
    {
        int lastFrame = (owner.CurrAnim != null) ? owner.CurrAnim.Steps.Count - 1 : 9;

        if (owner.CurrentFrameIndex == 5 && !_hasSetHit1)
        {
            owner.SetHitData(GetHitData(owner));
            _hasSetHit1 = true;
        }

        if (owner.CurrentFrameIndex == 2 && !_hasCreatedFx)
        {
            _hasCreatedFx = true;
            var fxEntity = new NarutoSkillBEntity(owner);
            EntityFactory.CreateSkillEntity(fxEntity);

            EffectData effect = new EffectData
            {
                EffectName = "skill2_smk",
                WorldPos = owner.pos + new Vector2(0, -0.01f),
                IsFacingLeft = owner.IsFacingLeft,
                Offset = new Vector2(0, 0)
            };

            EventManager.Instance.Emit("PlayEffect", effect);

        }

        if (owner.height > 0)
        {
            if (owner.CurrentFrameIndex >= lastFrame)
            {
                owner.CurrentFrameIndex = lastFrame;
                owner.LogicalTickCounter = 0;
            }
        }
        else if (!_isLand)
        {
            if (owner.CurrentFrameIndex >= lastFrame)
            {
                owner.velocity = Vector2.zero;
                _isLand = true;

                owner.SwitchAnimation("Skill_B_land");
            }
        }

        if (_isLand && owner.IsAnimEnd())
        {
            if(_isCom1Hitted)
            {
                owner.SwitchAnimation("Skill_B_2");
                _comIdx = 2;
                owner.SetHitData(GetHitData(owner));
            } else
            {
                owner.RootSM.ChangeState(CommonState.Idle);
            }
            
        }
    }

    private void UpdateCom2(CharacterEntity owner)
    {
        if(owner.IsAnimEnd())
        {
            owner.RootSM.ChangeState(CommonState.Idle);
        }
    }

    public HitData GetHitData(CharacterEntity owner)
    {
        HitData data = new HitData(HitType.Normal);
        data.Owner = owner;
        data.Player = owner;
        data.Pos = owner.pos;
        data.Height = owner.height;
        data.PushDirX = owner.IsFacingLeft ? -1f : 1f;

        data.Damage = 100;
        data.HitStun = 12;
        data.PushSpeed = 20;
        data.PushSpeedAir = 2f;

        //data.HitEffectName = "HitSpark_Normal";
        //data.HitEffectOffset = new Vector2(50f, 60f);

        if(_comIdx == 1)
        {
            data.HitCallBack = (_owner) =>
            {
                this._isCom1Hitted = true;
            };
        } else
        {
            data.HType = HitType.ToAir;
            data.IsHeavyHit = true;
            data.PushSpeedY = 40;
            data.PushSpeed = 10;
            data.PushSpeedAir = 2f;
        }
        return data;
    }


    public override void Exit(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
        owner.armorLevel = ArmorLevel.normal;
    }

}