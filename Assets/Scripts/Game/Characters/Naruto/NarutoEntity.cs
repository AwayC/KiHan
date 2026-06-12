using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;
using Managers;
using System;


/// <summary>
/// 鸣人逻辑实体
/// </summary>
/// 
public class NarutoEntity : CharacterEntity
{
    public override string Name => "Naruto";

    public override void Init()
    {
        // 1. 设置属性
        CharacterId = 90001; 
        Blood = 1200;
        MoveSpeed = 5f;

        // 2. 加载动画
        LoadRes("Characters/naruto/");

        Func<CharacterEntity, bool> inputHook = (owner) =>
        {
            var input = owner.CurrInput;
            if (input == null) return false;

            // 恢复为简单的状态检查：只要按着攻击键就进入攻击状态
            if ((input.Buttons & ButtonMask.Skill1) != 0)
            {
                Debug.Log("skill1 btn");
                owner.RootSM.ChangeState(NarutoState.SkillA);
                return true;
            }

            if ((input.Buttons & ButtonMask.Skill2) != 0)
            {
                Debug.Log("skill2 btn");
                owner.RootSM.ChangeState(NarutoState.SkillB);
                return true;
            }

            if ((input.Buttons & ButtonMask.Attack) != 0)
            {
                Debug.Log("attack btn");
                owner.RootSM.ChangeState(CommonState.Attack);
                return true;
            }

            if (input.JoyStickAngle != 255)
            {
                Debug.Log("run");
                owner.RootSM.ChangeState(CommonState.Run);
            }

            return false;
        };

        // 3. 组装状态机
        RootSM = new NarutoStateMachine(this);
        RootSM.RegisterState(new CommonStateIdle(inputHook));
        RootSM.RegisterState(new CommonStateRun(inputHook));
        RootSM.RegisterState(new CommonStateHurt());
        RootSM.RegisterState(new CommonStateLand()); // 注册落地收招状态
        RootSM.RegisterState(new NarutoStateAttack()); // 注册普攻状态
        RootSM.RegisterState(new NarutoStateSkillA()); // 注册1技能
        RootSM.RegisterState(new NarutoStateSkillB()); // 注册2技能

        

        // 4. 初始状态
        RootSM.ChangeState(CommonState.Idle);
    }

    private void RegisterAnim(string name, string path)
    {
        _animDict[name] = ResManager.Instance.Load<AnimationFrameData>(path + name);
    }

    public override void LoadRes(string basePath)
    {
        // 基础动画
        _animDict["Idle"] = ResManager.Instance.Load<AnimationFrameData>(basePath + "Idle");
        _animDict["Run"] = ResManager.Instance.Load<AnimationFrameData>(basePath + "Run");
        _animDict["Land"] = ResManager.Instance.Load<AnimationFrameData>(basePath + "Land"); // 主动落地（如4a）
        _animDict["Hurt_land"] = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_land"); // 击飞落地
        _animDict["Skill_A_1"] = ResManager.Instance.Load<AnimationFrameData>(basePath + "Skill_A_1");
        RegisterAnim("Skill_A_2", basePath);
        RegisterAnim("Skill_B_1", basePath);
        RegisterAnim("Skill_B_land", basePath);
        RegisterAnim("Skill_B_fx", basePath);
        RegisterAnim("Skill_B_2", basePath);

        // 普攻 4 段
        for (int i = 1; i <= 4; i++)
        {
            string key = $"Attack_{i}";
            var data = ResManager.Instance.Load<AnimationFrameData>(basePath + key);
            if (data != null) _animDict[key] = data;
        }

        // 受击 4 段 + 特殊受击
        for (int i = 1; i <= 4; i++)
        {
            string key = $"Hurt_{i}";
            var data = ResManager.Instance.Load<AnimationFrameData>(basePath + key);
            if (data != null) _animDict[key] = data;
        }

        // 加载击飞相关动画
        string[] extraHurts = { "Hurt_toair", "Hurt_inair", "Hurt_fall" };
        foreach (var key in extraHurts)
        {
            var data = ResManager.Instance.Load<AnimationFrameData>(basePath + key);
            if (data != null) _animDict[key] = data;
        }

        EffectManager.Instance.Preload("Characters/naruto/effect/lxw/ball", 1, "lxw");
        EffectManager.Instance.Preload("Characters/naruto/effect/fengshen/fengshen", 1, "fengshen");
        EffectManager.Instance.Preload("Characters/naruto/effect/skill2_smk/skill2_smk", 1, "skill2_smk");

        var sp = "Effect/Spark/";
        EffectManager.Instance.Preload(sp + "Spark1/Spark", 1, "Spark1");
        EffectManager.Instance.Preload(sp + "Spark5/Spark", 1, "Spark5");
    }

    //public override void ApplyHit(HitData hit)
    //{
    //    if (hit == null) return;
        
    //    // 简单判定：如果是普通受击（没有霸体）
    //    if (armorLevel == ArmorLevel.normal)
    //    {
    //        base.ApplyHit(hit);
    //    }
    //    else
    //    {
    //        // 霸体状态：只扣血，不产生硬直和状态切换
    //        this.LastHitData = hit;
    //        this.Blood -= Mathf.Max(1, (int)(hit.Damage / Defence));
    //    }
    //}
}

public class NarutoStateMachine : StateMachine
{
    public NarutoStateMachine(CharacterEntity owner) : base(owner) { }
}
