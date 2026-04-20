using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;
using Managers;

/// <summary>
/// 鸣人逻辑实体
/// </summary>
public class NarutoEntity : CharacterEntity
{
    public void Init()
    {
        string basePath = "Characters/naruto/";
        
        // 1. 加载动画资源
        var animIdle = ResManager.Instance.Load<AnimationFrameData>(basePath + "Idle");
        var animRun = ResManager.Instance.Load<AnimationFrameData>(basePath + "Run");
        var animLand = ResManager.Instance.Load<AnimationFrameData>(basePath + "Land");

        List<AnimationFrameData> attackAnims = new List<AnimationFrameData>();
        for (int i = 1; i <= 4; i++) {
            var a = ResManager.Instance.Load<AnimationFrameData>($"{basePath}Attack_{i}");
            if (a != null) attackAnims.Add(a);
        }

        List<AnimationFrameData> skillAAnims = new List<AnimationFrameData>();
        for (int i = 1; i <= 2; i++) {
            var s = ResManager.Instance.Load<AnimationFrameData>($"{basePath}Skill_A_{i}");
            if (s != null) skillAAnims.Add(s);
        }

        List<AnimationFrameData> hurtAnims = new List<AnimationFrameData>();
        var h1 = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_1");
        if (h1 != null) hurtAnims.Add(h1);

        // 2. 注入状态 (混合通用状态与特定状态)
        AddState(new CommonIdleState { Anim = animIdle });
        AddState(new CommonRunState { Anim = animRun });
        AddState(new CommonLandState { Anim = animLand });
        AddState(new CommonHurtState { Anims = hurtAnims });

        // 鸣人特有状态
        AddState(new NarutoAttackState { Anims = attackAnims });
        AddState(new NarutoSkillAState { Anims = skillAAnims });

        // 默认进入待机
        ChangeState(CommonState.Idle);
    }
}
