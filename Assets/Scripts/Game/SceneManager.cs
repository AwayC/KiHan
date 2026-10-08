using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using View.Component;
using KiHan.Asset;

/// <summary>
/// 表现层场景与视觉实体协调器
/// </summary>
public class SceneManager : UnitySingleton<SceneManager>
{
    private int my_gid;

    public void InitWorld()
    {
        my_gid = LockstepManager.Instance != null ? LockstepManager.Instance.MyGameId : 1;
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
