using KiHan.Logic;
using Managers;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum ArmorLevel : byte
{
    normal = 0, 
    skill = 1, 
    super = 2,
    kingkong = 3,
    notHitby = 4 // 虚化无敌
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
    public bool IsAirborne = false; // 记录是否处于击飞/浮空状态
    
    public virtual void UpdateInput(InputFrame input)
    {
        CurrInput = input;
    }

    public override void Tick()
    {
        // 优先更新时钟
        UpdateTickCounter();
        // 先更新状态机
        RootSM?.Update();

        // 更新物理
        ProcessPhysics();
    }

    public virtual void ApplyHit(HitData hit)
    {
        if (hit == null) return;

        Debug.Log("hit " + hit);

        // 1. 状态拦截（第一优先级）
        if (RootSM != null && RootSM.TryInterceptHit(hit))
        {
            return; // 状态选择自己消化这次受击，阻断默认流程
        }

        // 虚化无敌
        if (this.armorLevel == ArmorLevel.notHitby) return;

        // --- 默认受击流程 ---
        this.LastHitData = hit;

        // 触发受击特效 (如果攻击方配置了的话)
        if (!string.IsNullOrEmpty(hit.HitEffectName))
        {
            EffectData effect = new EffectData
            {
                EffectName = hit.HitEffectName,
                WorldPos = this.pos,
                Offset = hit.HitEffectOffset,
                IsFacingLeft = this.IsFacingLeft,
                Height = this.height,
                BindEntity = null // 打击火花通常不随人动，而是留在受击那一刻的位置
            };
            EventManager.Instance.Emit("PlayEffect", effect);
        }

        // 扣血计算 (伤害 / 防御)
        this.Blood -= Mathf.Max(1, (int)(hit.Damage / Defence));

        // 记录硬直时间
        this.StunTimer = hit.HitStun;

        // --- 触发伤害跳字特效 ---
        // 为了方便，这里假定由于我们没有传递 Attacker 引用，受击者的反方向即为抛物线方向
        // 且为了演示白/红字，假定只有 P1 (owner=1) 攻击时才会触发白字，但由于没传 attacker，
        // 我们可以根据当前受击者是不是 P1 来反推：如果是 P1 挨打，说明是敌人打的（红字）；如果是 P2 挨打，说明是玩家打的（白字）。
        bool isPlayerHit = this.owner != 1; 
        int hitDirection = this.IsFacingLeft ? 1 : -1; // 抛向受击者面朝的反方向

        var offset = 0.3f;
        int damageValue = hit.Damage > 0 ? hit.Damage : 0;
        Vector3 visualPos = new Vector3(this.pos.x, this.pos.y + this.height * 0.01f + offset, 0);

        // 调用 SceneManager 接口解耦逻辑层和表现层
        SceneManager.Instance.ShowDamageText(damageValue, isPlayerHit, visualPos, hitDirection);

        // 保存当前状态：是否被击飞或已经在空中
        if (hit.HType == HitType.ToAir || this.height > 0 || this.h_vel > 0)
        {
            this.IsAirborne = true;
        }

        // 切换到受击状态
        RootSM?.ChangeState(CommonState.Hurt);

        // 通知攻击者
        hit.CallHitOwner();

    }
}
