using System.Collections.Generic;
using UnityEngine;
using KiHan.Asset;
using KiHan.Config;
using KiHan.Logic;
using KiHan.View.Render;
using Managers;
using View.Component;

/// <summary>
/// 战斗表现层协调器：为逻辑角色与生成物创建 / 复用视图，每个逻辑帧后推进视图快照，
/// 并把逻辑事件转换为表现：动作事件 (特效 / 镜头)、打击事件 (火花 / 跳字 / 震屏)
/// 只读逻辑数据，不回写
/// </summary>
public class SceneManager : UnitySingleton<SceneManager>
{
    private const string HitSparkEffect = "Effect/Spark/Spark1/Spark";

    /// <summary>
    /// 开局预热数量：同屏可能同时存在的命中火花、动作特效与伤害跳字上限
    /// </summary>
    private const int HitSparkPrewarm = 8;
    private const int ActionEffectPrewarm = 2;
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
    /// 生成物实例 -> 视图 (实例与视图一一对应，随实例池复用)
    /// </summary>
    private readonly Dictionary<CharacterObj, ActorView> _spawnViews = new Dictionary<CharacterObj, ActorView>();

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

    /// <summary>
    /// 预热角色动作事件与 Buff 引用的特效 (加载阶段，每个资源预建 ActionEffectPrewarm 个实例)
    /// 特效 key 在角色包资源表中找到时登记包内预制体 (地址 pkg/{id}/{key})，否则按全局 Resources 路径加载
    /// </summary>
    public void PreloadActionEffects(CharacterTemplate template)
    {
        var assets = new HashSet<string>();
        foreach (ActionInfo action in template.Actions)
        {
            if (action.events == null) continue;
            foreach (ActionEventInfo evt in action.events)
            {
                if (evt.type == ActionEventType.Effect) Preload(template.Manifest, evt.asset, assets);
            }
        }
        foreach (BuffInfo buff in template.Buffs.Values) Preload(template.Manifest, buff.effect, assets);
    }

    private static void Preload(CharacterManifest manifest, string key, HashSet<string> loaded)
    {
        if (string.IsNullOrEmpty(key) || !loaded.Add(key)) return;
        string address = key;
        if (manifest != null && manifest.TryGetPrefab(key, out string packaged, out GameObject prefab))
        {
            ObjectPoolManager.Instance.RegisterPrefab(packaged, prefab);
            address = packaged;
        }
        EffectManager.Instance.Preload(address, ActionEffectPrewarm);
    }

    /// <summary>
    /// 特效 key -> 播放地址 (与预热时一致；包内地址由清单缓存，不产生 GC)
    /// </summary>
    private static string ResolveEffect(int characterId, string key)
    {
        CharacterManifest manifest = CharacterRegistry.Find(characterId);
        return manifest != null && manifest.TryGetPrefab(key, out string address, out _) ? address : key;
    }

    /// <summary>
    /// 为预建的生成物实例预建视图 (加载阶段，视图隐藏待用)
    /// </summary>
    public void PrewarmSpawnViews(IReadOnlyList<CharacterObj> instances)
    {
        for (int i = 0; i < instances.Count; i++)
        {
            GetOrCreateSpawnView(instances[i]).Hide();
        }
    }

    public ActorView SpawnView(CharacterObj character)
    {
        ActorView view = ActorView.Create(character);
        ViewManager.Instance.Register(view);
        return view;
    }

    /// <summary>
    /// 每个逻辑帧结算后调用 (逻辑事件只保留当前帧，必须逐帧消费)
    /// 顺序：新生成物出现 -> 推进视图快照 -> 动作事件 -> 打击事件 -> 回收的生成物隐藏
    /// </summary>
    public void OnLogicTick(BattleWorld world)
    {
        for (int i = 0; i < world.Spawned.Count; i++)
        {
            CharacterObj entity = world.Spawned[i];
            ActorView view = GetOrCreateSpawnView(entity);
            view.Rebind(entity);
            ViewManager.Instance.Register(view);
        }

        foreach (ActorView view in ViewManager.Instance.Views)
        {
            view.OnLogicTick();
        }

        for (int i = 0; i < world.ViewEvents.Count; i++)
        {
            PresentViewEvent(world.ViewEvents[i]);
        }

        for (int i = 0; i < world.HitEvents.Count; i++)
        {
            PresentHit(world.HitEvents[i]);
        }

        for (int i = 0; i < world.Despawned.Count; i++)
        {
            ActorView view = ViewManager.Instance.Unregister(world.Despawned[i]);
            if (view != null) view.Hide();
        }
    }

    public void Clear()
    {
        // 隐藏中的生成物视图不在 ViewManager 中，单独销毁 (登记中的由 ClearAll 处理)
        foreach (ActorView view in _spawnViews.Values)
        {
            if (view == null || view.Character != null) continue;
            view.Release();
            Destroy(view.gameObject);
        }
        _spawnViews.Clear();

        ViewManager.Instance.ClearAll();
        _damageTexts?.Dispose();
        _damageTexts = null;
    }

    private ActorView GetOrCreateSpawnView(CharacterObj entity)
    {
        if (_spawnViews.TryGetValue(entity, out ActorView view)) return view;
        view = ActorView.Create(entity);
        _spawnViews[entity] = view;
        return view;
    }

    private void PresentViewEvent(in ViewEvent evt)
    {
        switch (evt.Type)
        {
            case ActionEventType.Effect:
            case ActionEventType.Buff:
                PlayActionEffect(evt);
                break;
            case ActionEventType.BuffRemoved:
                ActorView view = ViewManager.Instance.GetView(evt.EntityId);
                if (view != null) view.StopBuffEffect(evt.BuffSerial);
                break;
            case ActionEventType.Camera:
                CameraControllor.Instance.ImpactEffect(evt.Asset == CameraEffects.ImpactHeavy);
                break;
        }
    }

    /// <summary>
    /// 动作特效 / Buff 特效：跟随型挂到触发者 Body 下并逐帧对齐锚点；独立型放在触发时的锚点位置
    /// 动作特效可在触发者的动作结束时提前停止 (stopOnActionEnd)，Buff 特效在 Buff 到期时停止
    /// </summary>
    private void PlayActionEffect(in ViewEvent evt)
    {
        ActorView owner = ViewManager.Instance.GetView(evt.EntityId);
        bool follow = evt.Bind && owner != null;
        bool buff = evt.Type == ActionEventType.Buff;

        var data = new EffectData { EffectName = ResolveEffect(evt.CharacterId, evt.Asset), IsFacingLeft = evt.FacingLeft };
        if (follow)
        {
            data.BindView = owner;
        }
        else
        {
            data.WorldPos = BattleViewSpace.ToWorld(evt.Position) + Vector3.back * BattleViewSpace.ActionEffectOffset;
        }

        int id = EffectManager.Instance.PlayEffect(data, evt.Loop);
        if (id < 0 || owner == null || (!follow && !evt.StopOnActionEnd && !buff)) return;

        owner.AttachEffect(id, follow, evt.Anchor, evt.Offset, evt.ActionSerial, evt.StopOnActionEnd, buff ? evt.BuffSerial : 0);
    }

    private void PresentHit(HitEvent evt)
    {
        ActorView attacker = ViewManager.Instance.GetView(evt.AttackerId);
        ActorView owner = ViewManager.Instance.GetView(evt.OwnerId);
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
            // 生成物造成的伤害按其主人区分颜色
            bool byLocalPlayer = owner != null && owner.Character.InputSlot == _localSlot;
            _damageTexts.Show(damage, byLocalPlayer, pos, attackerFacingLeft ? -1 : 1);
        }

        CameraControllor.Instance.ImpactEffect(evt.Reaction == HitReaction.Launch);
    }
}
