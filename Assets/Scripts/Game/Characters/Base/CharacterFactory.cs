using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterFactory
{
    public static PlayerView CreatePlayer(byte gId, int charId, Vector2 pos)
    {
        // 从角色id，映射获取角色类型，并创建
        System.Type T = CharacterConfig.GetCharacterType(charId);
        object[] constructorArgs = new object[] {};
        CharacterEntity logic = (CharacterEntity)System.Activator.CreateInstance(T, constructorArgs);

        logic.owner = gId;
        logic.pos = pos;
        logic.IsFacingLeft = (gId == 2);

        // TODO: 后续可以抽象一个 IInitializable 接口，这里为了简单先强转处理特有初始化
        logic.Init();

        // 交给 Room 统一管理并分配 ID
        BattleManager.Instance.ActiveRoom?.AddPlayer(logic);

        // 创建表现层
        GameObject viewGo = new GameObject($"{T.Name}_View_{gId}");
        viewGo.transform.position = new Vector3(logic.pos.x, logic.pos.y, 0);

        var view = viewGo.AddComponent<PlayerView>();
        view.BindEntity = logic;

        return view;
    }

   
}
