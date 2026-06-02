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

        EffectData effect = new EffectData
        {
            EffectName = "lxw",
            AnchorName = "lxw",
            BindEntity = owner,
            IsFacingLeft = owner.IsFacingLeft
        };

        EffectData effectFeng = new EffectData
        {
            EffectName = "fengshen",
            BindEntity = owner,
            IsFacingLeft = owner.IsFacingLeft,
            Offset = new Vector2(-110, 80)
        };

        EventManager.Instance.Emit("PlayEffect", effect);
        EventManager.Instance.Emit("PlayEffect", effectFeng);
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
        data.PushSpeedAir = 0f; // ����׷��ʱ��ˮƽ���ˣ���Сһ���ֹ���̫Զ�Ӳ��ϣ�
        data.HitEffectName = "Spark1";
        data.HitEffectOffset = new Vector2(-160, 400);

        // Ĭ�ϴ�ֱ�ٶȣ�Ϊ��֧�ֿ���׷�� (Juggle)����ͨ����Ҳ��һ���������
        data.PushSpeedY = 10;
        if (owner.CurrentFrameIndex == 5)
        {
            data.HType = HitType.ToAir;
            data.HitStun = 40;
            data.PushSpeed = 0f;
            data.PushSpeedAir = 0f;
            data.PushSpeedY = 55; // 4a ���ɸ���
            data.HitEffectName = "Spark5";
            data.IsHeavyHit = true; // �����ػ�����
            data.HitEffectOffset = new Vector2(0, 100);
        }
        return data;
    }


    public override void Exit(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
    }

}