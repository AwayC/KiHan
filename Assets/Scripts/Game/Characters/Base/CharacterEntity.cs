using KiHan.Logic;
using Managers;
using System.Collections.Generic;

public enum ArmorLevel : byte
{
    normal = 0, 
    skill = 1, 
    super = 2,
    kingkong = 3,
}

/// <summary>
/// 角色实体 (带状态机)
/// </summary>
/// 
public abstract class CharacterEntity : LogicEntity
{
    // --- 属性 ---
    public abstract string Name { get; }
    public int Id;
    public int Blood = 1000;
    public float Attack = 1.0f;  // 伤害加成
    public float Defence = 1.0f; // 防御减免

    public float MoveSpeed = 10.0f;
    
    public StateMachine RootSM;
    public InputFrame CurrInput;

    public HitData LastHitData;
    public ArmorLevel armorLevel;
    
    public virtual void UpdateInput(InputFrame input)
    {
        CurrInput = input;
    }

    public override void Tick()
    {
        RootSM?.Update();

        base.Tick();
    }

    public override HitData GetHitData()
    {
        return RootSM?.GetHitData();
    }

    public virtual void ApplyHit(HitData hit)
    {
        if (hit == null) return;

        RootSM?.ChangeState(CommonState.Hurt);
    }
}
