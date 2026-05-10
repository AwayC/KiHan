using KiHan.Logic;
using Managers;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

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
    // --- 属性 ---
    public abstract string Name { get; }
    public int Id;
    public int CharacterId; // 资源关联 ID (如 90001)
    public int Blood = 1000;
    public int StunTimer = 0;   // 硬直计时器 (逻辑帧单位)
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
        // 先更新状态机
        RootSM?.Update();

        // 再更新逻辑（位移、动画等）
        base.Tick();
    }

    public override HitData GetHitData()
    {
        return RootSM?.GetHitData();
    }

    public virtual void ApplyHit(HitData hit)
    {
        if (hit == null) return;

        this.LastHitData = hit;
        
        // 扣血计算 (伤害 / 防御)
        this.Blood -= Mathf.Max(1, (int)(hit.Damage / Defence));

        // 记录硬直时间
        this.StunTimer = hit.HitStun;

        // 切换到受击状态
        RootSM?.ChangeState(CommonState.Hurt);
    }
}
