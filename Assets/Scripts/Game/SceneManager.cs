using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using View;
using View.Component;

public class SceneManager : UnitySingleton<SceneManager>
{
    private int my_gid;

    public void InitWorld()
    {
        // 主要是清空之前的表现层残留
        my_gid = LockstepManager.Instance.MyGameId;
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
    /// 提供给逻辑层调用的接口，用于显示伤害飘字
    /// </summary>
    public void ShowDamageText(int damageValue, int gameId, Vector3 visualPos, int hitDirection)
    {
        GameObject textGo = new GameObject("DamageText");
        var textNode = textGo.AddComponent<DamageTextNode>();
        textNode.Init(damageValue, gameId != my_gid, visualPos, hitDirection);
    }
}
