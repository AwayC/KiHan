using System;
using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 一名玩家的出战配置 (角色 + 佩戴的秘卷与通灵)
    /// </summary>
    [Serializable]
    public class PlayerLoadout
    {
        [Tooltip("角色 ID (对应角色注册表中的角色包)")]
        public int characterId = 90001;

        [Tooltip("佩戴的秘卷 id (秘卷表 Resources/Config/SecretScrollRegistry)，为空不佩戴")]
        public string secret = string.Empty;

        [Tooltip("佩戴的通灵 id (通灵系统尚未实现：目前只按数量显示通灵键上的豆)")]
        public string[] summons = Array.Empty<string>();
    }
}
