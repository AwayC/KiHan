using KiHan.Logic;
using Managers;
using System.Collections.Generic;

/// <summary>
/// 角色实体 (带状态机)
/// </summary>
public class CharacterEntity : LogicEntity
{
    public float MoveSpeed = 5.0f;
    private Dictionary<sbyte, EntityState> _states = new Dictionary<sbyte, EntityState>();
    public EntityState CurrentState { get; private set; }

    public void AddState(EntityState state) => _states[state.StateType] = state;

    public override void ChangeState(sbyte type)
    {
        if (_states.TryGetValue(type, out var next))
        {
            CurrentState?.Exit(this);
            CurrentState = next;
            CurrentState.Enter(this);
        }
    }

    public override void Tick(InputFrame input)
    {
        CurrentState?.Update(this, input);
        base.Tick(input);
    }

    public virtual void LoadCommonRes(string basePath)
    {
        var animIdle  = ResManager.Instance.Load<AnimationFrameData>(basePath + "Idle");
        var animRun   = ResManager.Instance.Load<AnimationFrameData>(basePath + "Run");
        var animLand  = ResManager.Instance.Load<AnimationFrameData>(basePath + "Land");

        List<AnimationFrameData> hurtAnims = new List<AnimationFrameData>();
        for (int i = 1;i <= 4;i ++)
        {
            var h = ResManager.Instance.Load<AnimationFrameData>($"{basePath}Hurt_{i}");
            if (h != null) hurtAnims.Add(h);
        }

        var animHurtCmd    = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_cmd");
        var animHurtFall   = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_fall");
        var animHurtInair  = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_inair");
        var animHurtToair  = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_toair");
        var animHurtLand   = ResManager.Instance.Load<AnimationFrameData>(basePath + "Hurt_Land");

        AddState(new CommonIdleState { Anim = animIdle });
        AddState(new CommonRunState { Anim = animRun });
        AddState(new CommonLandState { Anim = animLand });
        AddState(new CommonHurtState { Anims = hurtAnims });

        // 默认进入待机
        ChangeState(CommonState.Idle);
    }
}
