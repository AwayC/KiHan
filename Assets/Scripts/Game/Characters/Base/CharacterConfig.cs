using System.Collections;
using System.Collections.Generic;
using UnityEngine;

enum CharacterId : int
{
    Naruto = 90001,
}

public class CharacterConfig
{
    private static readonly Dictionary<int, System.Type> _typeTable = new Dictionary<int, System.Type>()
        {
            { 90001, typeof(NarutoEntity) }, // 90001 -> 鸣人脚本类
        };

    /// <summary>
    /// 根据数值ID直接获取对应的C#类类型
    /// </summary>
    public static System.Type GetCharacterType(int charId)
    {
        if (_typeTable.TryGetValue(charId, out System.Type targetType))
        {
            return targetType;
        }

        Debug.LogError($"[Registry] 未能找到ID为 {charId} 的角色类型映射！自动降级为通用基础类。");
        return typeof(CharacterEntity); // 找不到时返回基类兜底，防止直接崩溃
    }
}
