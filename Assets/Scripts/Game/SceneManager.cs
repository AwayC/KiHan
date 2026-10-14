using UnityEngine;
using KiHan.Logic;
using KiHan.View.Render;
using Managers;
using View.Component;

/// <summary>
/// 战斗表现层协调器：为逻辑角色创建视图，每个逻辑帧后推进视图快照，并把逻辑打击事件转换为火花、跳字与震屏
/// 只读逻辑数据，不回写
/// </summary>
public class SceneManager : UnitySingleton<SceneManager>
{
    private const string HitSparkEffect = "Effect/Spark/Spark1/Spark";

    /// <summary>
    /// 开局预热数量：同屏可能同时存在的命中火花与伤害跳字上限
    /// </summary>
    private const int HitSparkPrewarm = 8;
    private const int DamageTextCapacity = 16;

    /// <summary>
    /// Spark1 贴图内容不在画布中心：主体竖向光束 (第 2~5 帧) 中心相对枢轴约 (-156, -263) 像素，预制体缩放 0.8；
    /// 反向偏移使光束中心落在命中点上
    /// </summary>
    private static readonly Vector2 HitSparkOffset = new Vector2(125f, 210f);

    /// <summary>
    /// 本机玩家的输入槽位 (区分己方造成的伤害：白字，受到的伤害：红字)
    /// </summary>
    private int _localSlot;

    private DamageTextPool _damageTexts;

    /// <summary>
    /// 加载阶段调用：记录本机槽位，预热局内会用到的表现资源 (局内不再 Load / 新建实例)
    /// </summary>
    public void InitWorld(int localSlot)
    {
        _localSlot = localSlot;
        EffectManager.Instance.Preload(HitSparkEffect, HitSparkPrewarm);
        _damageTexts?.Dispose();
        _damageTexts = new DamageTextPool(DamageTextCapacity);
    }

    public ActorView SpawnView(CharacterObj character)
    {
        ActorView view = ActorView.Create(character);
        ViewManager.Instance.Register(view);
        return view;
    }

    /// <summary>
    /// 每个逻辑帧结算后调用 (HitEvents 只保留当前帧，必须逐帧消费)
    /// </summary>
    public void OnLogicTick(BattleWorld world)
    {
        foreach (ActorView view in ViewManager.Instance.Views)
        {
            view.OnLogicTick();
        }

        for (int i = 0; i < world.HitEvents.Count; i++)
        {
            PresentHit(world.HitEvents[i]);
        }
    }

    public void Clear()
    {
        ViewManager.Instance.ClearAll();
        _damageTexts?.Dispose();
        _damageTexts = null;
    }

    private void PresentHit(HitEvent evt)
    {
        ActorView attacker = ViewManager.Instance.GetView(evt.AttackerId);
        bool attackerFacingLeft = attacker != null && attacker.Character.FacingLeft;

        // 前景表现朝相机 (-Z) 前移，保证位于参与命中的双方之前
        Vector3 pos = BattleViewSpace.ToWorld(evt.HitPoint) + Vector3.back * BattleViewSpace.ForegroundOffset;

        EffectManager.Instance.PlayEffect(new EffectData
        {
            EffectName = HitSparkEffect,
            WorldPos = pos,
            Offset = HitSparkOffset,
            IsFacingLeft = attackerFacingLeft
        });

        int damage = Mathf.RoundToInt(evt.Damage.AsFloat());
        if (damage > 0 && _damageTexts != null)
        {
            bool byLocalPlayer = attacker != null && attacker.Character.InputSlot == _localSlot;
            _damageTexts.Show(damage, byLocalPlayer, pos, attackerFacingLeft ? -1 : 1);
        }

        CameraControllor.Instance.ImpactEffect(evt.Reaction == HitReaction.Launch);
    }
}
