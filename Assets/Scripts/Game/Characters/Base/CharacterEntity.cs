using KiHan.Logic;
using Managers;
using System.Collections.Generic;
using Unity.VisualScripting;

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
public abstract class CharacterEntity : LogicEntity
{
    public virtual string Name { get; }
    public int Blood { get; set; }
    public float Attack { get; set; }
    public float Defence { get; set; }
    public StateMachine RootSM;
    public InputFrame CurrInput = null;
    public float MoveSpeed;
    public HitData LastHitData = null;

    public int StunTime = 0; // 僵直时间
    public int StunTimer = 0; // 僵直计数器

    public ArmorLevel armorLever = ArmorLevel.normal;

    public virtual void UpdateInput(InputFrame input)
    {
        CurrInput = input;
    }

    public override void Tick()
    {
        base.Tick();
    }

    public abstract void ApplyHit(HitData hit);
}
