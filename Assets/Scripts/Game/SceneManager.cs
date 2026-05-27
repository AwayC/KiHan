using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using View;

public class SceneManager : UnitySingleton<SceneManager>
{
    private Dictionary<byte, CharacterEntity> _players = new Dictionary<byte, CharacterEntity>();
    private List<LogicEntity> _allEntities = new List<LogicEntity>();
    
    // 延迟添加和删除列表，防止在 Tick 循环中修改集合报错
    private List<LogicEntity> _pendingAdd = new List<LogicEntity>();
    private List<LogicEntity> _pendingRemove = new List<LogicEntity>();

    private int _nextEntityId = 1;

    public void InitWorld()
    {
        _players.Clear();
        _allEntities.Clear();
        _pendingAdd.Clear();
        _pendingRemove.Clear();
        _nextEntityId = 1;
    }

    public PlayerView SpawnPlayer(byte gId, Vector2 pos, GameObject playerViewPrefab, View.UI.BattleUIPanel combatUI, byte myGameId)
    {
        Debug.Log($"[SceneManager] 生成玩家: {gId} 于 {pos}");
        
        // 使用工厂模式创建玩家实体和表现层
        PlayerView view = EntityFactory.CreatePlayer<NarutoEntity>(gId, pos, playerViewPrefab);
        
        // --- 核心：当本地角色“加载”时，同步 UI 按键图标 ---
        if (gId == myGameId && combatUI != null)
        {
            combatUI.SetupIcons(((CharacterEntity)view.BindEntity).CharacterId);
        }

        return view;
    }

    /// <summary>
    /// 将玩家实体注册到管理器中
    /// </summary>
    public void AddPlayer(CharacterEntity player)
    {
        if (player.EntityId <= 0)
        {
            player.EntityId = _nextEntityId++;
        }
        _players[(byte)player.owner] = player;
        _allEntities.Add(player);
    }

    public CharacterEntity GetPlayer(byte gId)
    {
        if (_players.TryGetValue(gId, out var player))
        {
            return player;
        }
        return null;
    }

    /// <summary>
    /// 动态添加实体（如技能产生的飞行道具），安全的延迟添加
    /// </summary>
    public void AddEntity(LogicEntity entity)
    {
        if (entity.EntityId <= 0)
        {
            entity.EntityId = _nextEntityId++;
        }
        _pendingAdd.Add(entity);
    }

    /// <summary>
    /// 动态移除实体，安全的延迟移除
    /// </summary>
    public void RemoveEntity(LogicEntity entity)
    {
        _pendingRemove.Add(entity);
    }

    public void ApplyInputs(Dictionary<byte, InputFrame> inputFrames)
    {
        foreach (var kv in inputFrames)
        {
            if (_players.TryGetValue(kv.Key, out var player))
            {
                player.UpdateInput(kv.Value);
            }
        }
    }

    public void TickAll()
    {
        // 1. 处理延迟添加
        if (_pendingAdd.Count > 0)
        {
            _allEntities.AddRange(_pendingAdd);
            _pendingAdd.Clear();
        }

        // 2. 逻辑 Tick (更新物理与逻辑帧)
        for (int i = 0; i < _allEntities.Count; i++)
        {
            _allEntities[i].Tick();
        }

        // 3. 处理延迟删除
        if (_pendingRemove.Count > 0)
        {
            foreach(var entity in _pendingRemove)
            {
                _allEntities.Remove(entity);
                // 可以在这里调用 entity.OnDestroy() 通知逻辑层清理
            }
            _pendingRemove.Clear();
        }

        // 4. 碰撞检测
        if (_players.TryGetValue(1, out var p1)) DoCollisionCheck(p1);
        if (_players.TryGetValue(2, out var p2)) DoCollisionCheck(p2);
    }

    private void DoCollisionCheck(CharacterEntity target)
    {
        foreach (var attacker in _allEntities)
        {
            if (attacker.owner == target.owner) continue;
            
            // 判定：攻击者是否有攻击盒，且目标是否有受击盒，且未被此动作命中过
            if (attacker.CheckHit(target))
            {
                // Debug.Log("check hit " + Time.fixedTime + " " + attacker.CurrentFrameIndex + " " + attacker.LogicalTickCounter);
                if (attacker.CanHit(target))
                {
                    HitData hitData = attacker.GetHitData();
                    target.ApplyHit(hitData);
                    attacker.RegisterHit(target); // 标记命中，防止同一段动作重复打击

                    // 触发相机打击感反馈
                    CameraControllor.Instance.ImpactEffect(hitData.IsHeavyHit);
                }
            }
        }
    }
}
