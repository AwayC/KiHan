using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;
using Managers;


/// <summary>
/// 鸣人逻辑实体
/// </summary>
/// 
public class NarutoEntity : CharacterEntity
{
    public override string Name => "Naruto";

    public void Init()
    {
        // 1. 设置属性
        CharacterId = 90001; 
        Blood = 1200;
        MoveSpeed = 5f;

        // 2. 加载动画
        LoadRes("Characters/naruto/");

        // 3. 组装状态机
        RootSM = new NarutoStateMachine(this);
        RootSM.RegisterState(new NarutoStateIdle());
        RootSM.RegisterState(new CommonStateRun());
        RootSM.RegisterState(new CommonStateHurt());
        RootSM.RegisterState(new CommonStateLand()); // 注册落地收招状态
        RootSM.RegisterState(new NarutoStateAttack()); // 注册普攻状态
        RootSM.RegisterState(new NarutoStateSkillA()); // 注册1技能

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
    }

    public override void ApplyHit(HitData hit)
    {
        if (hit == null) return;
        
        // 简单判定：如果是普通受击（没有霸体）
        if (armorLevel == ArmorLevel.normal)
        {
            base.ApplyHit(hit);
        }
        else
        {
            // 霸体状态：只扣血，不产生硬直和状态切换
            this.LastHitData = hit;
            this.Blood -= Mathf.Max(1, (int)(hit.Damage / Defence));
        }
    }
}

public class NarutoStateMachine : StateMachine
{
    public NarutoStateMachine(CharacterEntity owner) : base(owner) { }
}
