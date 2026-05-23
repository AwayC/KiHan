using KiHan.Logic;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NarutoStateSkillA : StateBase
{
    public override sbyte StateType => NarutoState.SkillA;

    public override void Enter(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
        
        owner.ForceLoop = false;
        owner.SwitchAnimation("Skill_A_1");
    }

    public override void Update(CharacterEntity owner)
    {
        if ((owner.CurrentFrameIndex == 3 || owner.CurrentFrameIndex == 4) && owner.LogicalTickCounter % 2 == 0)
        {
            owner.SetHitData(GetHitData(owner));
        }

        if (owner.CurrentFrameIndex == 5 && owner.LogicalTickCounter == 0)
        {
            owner.SetHitData(GetHitData(owner));
        }
        
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
        data.PushSpeed = 0;
        data.PushSpeedAir = 0f; // 空中追击时的水平击退（稍小一点防止打飞太远接不上）

        // 默认垂直速度：为了支持空中追击 (Juggle)，普通攻击也带一点点向上力
        data.PushSpeedY = 10;
        if (owner.CurrentFrameIndex == 5)
        {
            data.HType = HitType.ToAir;
            data.HitStun = 40;
            data.PushSpeed = 0f;
            data.PushSpeedAir = 0f;
            data.PushSpeedY = 55; // 4a 击飞更高
            data.IsHeavyHit = true; // 触发重击连震
        }
        return data;
    }


    public override void Exit(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
    }

}