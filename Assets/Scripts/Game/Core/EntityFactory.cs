using KiHan.Logic;
using UnityEngine;
using View;

public class EntityFactory
{
    /// <summary>
    /// 创建角色实体及表现层
    /// </summary>
    public static PlayerView CreatePlayer<T>(byte gId, Vector2 pos) where T : CharacterEntity, new()
    {
        T logic = new T();
        logic.owner = gId;
        logic.pos = pos;
        logic.IsFacingLeft = (gId == 2);

        // TODO: 后续可以抽象一个 IInitializable 接口，这里为了简单先强转处理特有初始化
        logic.Init();
        
        // 交给 Room 统一管理并分配 ID
        BattleManager.Instance.ActiveRoom?.AddPlayer(logic);
        
        // 创建表现层
        GameObject viewGo = new GameObject($"{typeof(T).Name}_View_{gId}");
        viewGo.transform.position = new Vector3(logic.pos.x, logic.pos.y, 0);
        
        var view = viewGo.AddComponent<PlayerView>();
        view.BindEntity = logic;
        
        return view;
    }

    /// <summary>
    /// 创建技能衍生实体及表现层
    /// </summary>
    public static EntityView CreateSkillEntity<T>(T logicEntity, bool autoAdd = true) where T : SkillDerivedEntity
    {
        // 交给 Room 统一管理并分配 ID
        if (autoAdd)
        {
            BattleManager.Instance.ActiveRoom?.AddEntity(logicEntity);
        }

        // 创建表现层
        GameObject viewGo = new GameObject($"{typeof(T).Name}_View_{logicEntity.EntityId}");
        viewGo.transform.position = new Vector3(logicEntity.pos.x, logicEntity.pos.y, 0);
        
        var view = viewGo.AddComponent<EntityView>();
        view.BindEntity = logicEntity;

        // 手动提前注册，避免由于 Unity 生命周期导致的 GetEntityView 为空问题
        Transform displayRoot = viewGo.transform.Find("Display");
        if (displayRoot != null)
        {
            Managers.ViewManager.Instance.RegisterView(logicEntity, displayRoot, view);
        }

        return view;
    }
}
