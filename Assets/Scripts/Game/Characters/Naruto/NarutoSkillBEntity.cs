using KiHan.Logic;
using Managers;
using UnityEngine;
using View;

public class NarutoSkillBEntity : SkillDerivedEntity
{
    private HitData _hitData;
    private bool _hasSetHit0 = false;
    private bool _hasSetHit5 = false;
    private bool _hasSetHit11 = false;

    public NarutoSkillBEntity(CharacterEntity creator) 
        : base(creator, 100) // 动态创建，直接给 100 寿命
    {
        // 直接从创建者（NarutoEntity）那里获取预加载好的动画，避免动态加载资源
        this.CurrAnim = creator.GetAnim("Skill_B_fx");
        if (this.CurrAnim == null)
        {
            Debug.LogError("[NarutoSkillBEntity] CurrAnim is null! Failed to get 'Skill_B_fx' from creator.");
        }
        else
        {
            Debug.Log($"[NarutoSkillBEntity] Created successfully. Anim Steps: {this.CurrAnim.Steps.Count}");
        }
        
        this.CurrentFrameIndex = 0;
        this.LogicalTickCounter = 0;
        
        // 初始位置和朝向
        this.pos = Creator.pos + new Vector2(0, -0.01f);
        this.height = Creator.height;
        this.IsFacingLeft = Creator.IsFacingLeft;
        
        // 预先构造 HitData，放在实体内部
        _hitData = new HitData(HitType.Normal);
        _hitData.Owner = creator;
        _hitData.Player = creator;
        _hitData.Damage = 50;
        _hitData.HitStun = 10;
        _hitData.PushSpeed = 25;
        _hitData.PushSpeedAir = 3f;
        _hitData.HitEffectOffset = new Vector2(-160, 400);
        _hitData.HitEffectName = "Spark1";


        _hitData.Pos = this.pos;
        _hitData.Height = this.height;
        _hitData.PushDirX = this.IsFacingLeft ? -1f : 1f;

        

        // 注意：不在这里使用工厂和 SceneManager 添加，而是像以前一样在状态机里 new 完后调用 EntityFactory.CreateSkillEntity 托管
    }

    public override void Tick()
    {
        base.Tick();

        // 到达指定帧时设置HitData
        if (this.CurrentFrameIndex == 0 && !_hasSetHit0)
        {
            this.SetHitData(_hitData);
            _hasSetHit0 = true;
        }

        if(this.CurrentFrameIndex == 5 && !_hasSetHit5)
        {
            this.SetHitData(_hitData);
            _hasSetHit5 = true;
        }

        if(this.CurrentFrameIndex == 11 && !_hasSetHit11)
        {
            this.SetHitData(_hitData);
            _hasSetHit11 = true;
        }

        if(this.height > 0)
        {
            if(this.CurrentFrameIndex >= 16)
            {
                this.CurrentFrameIndex = 16;
                // 注意：在空中卡在16帧时，我们不减去 Duration，而是直接清零，
                // 因为这相当于时间暂停，直到落地。
                this.LogicalTickCounter = 0;
            }
        } else
        {
            // 落地时，如果停留在16帧，则推进到17帧
            // 注意：这里改成了 == 16。如果是 >= 16，会导致在17帧时也被无限重置，动画永远无法结束
            if(this.CurrentFrameIndex == 16)
            {
                this.CurrentFrameIndex = 17;
                // 落地推进时，继承溢出的时间，保证平滑
                if (this.CurrAnim != null && this.CurrAnim.Steps.Count > 16)
                {
                   this.LogicalTickCounter -= this.CurrAnim.Steps[16].Duration;
                   if (this.LogicalTickCounter < 0) this.LogicalTickCounter = 0;
                }
                else 
                {
                   this.LogicalTickCounter = 0;
                }
            }
        }

        // 如果动画结束，销毁自身
        if (this.IsAnimEnd())
        {
            EffectData effect = new EffectData
            {
                EffectName = "skill2_smk",
                WorldPos = this.pos + new Vector2(0, -0.01f),
                IsFacingLeft = this.IsFacingLeft,
                Offset = new Vector2(0, 0)
            };

            EventManager.Instance.Emit("PlayEffect", effect);
            this.DestroySelf();
        }
    }

    public override void DestroySelf()
    {
        ClearHitData();
        
        // 从逻辑更新中移除
        base.DestroySelf();
        
        // 动态创建的模式下，播放完毕直接把表现层 GameObject 销毁掉
        var view = ViewManager.Instance.GetEntityView(this);
        if (view != null)
        {
            GameObject.Destroy(view.gameObject);
        }
    }
}
