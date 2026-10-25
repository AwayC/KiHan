using System;
using System.Collections.Generic;
using KiHan.Asset;
using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 角色配置表：一个角色 ID 对应一行 (资源目录、显示名、头像)，新增角色只加一行，代码里不出现具体角色
    /// 资源位置：Resources/Config/CharacterTable.asset
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterTable", menuName = "KiHan/Character Table")]
    public class CharacterTable : ScriptableObject
    {
        public const string ResourcePath = "Config/CharacterTable";

        public List<CharacterEntry> characters = new List<CharacterEntry>();

        private static CharacterTable _instance;

        /// <summary>
        /// 首次访问时加载 (配置表常驻：只有几行数据与头像引用)
        /// </summary>
        public static CharacterTable Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetManager.Instance.Load<CharacterTable>(ResourcePath);
                    if (_instance == null) Debug.LogError($"[CharacterTable] 找不到角色配置表: Resources/{ResourcePath}");
                }
                return _instance;
            }
        }

        /// <summary>
        /// 按角色 ID 查找，没有返回 null
        /// </summary>
        public CharacterEntry Find(int id)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (characters[i].id == id) return characters[i];
            }
            return null;
        }
    }

    [Serializable]
    public class CharacterEntry
    {
        [Tooltip("角色 ID (如 90001)")]
        public int id;

        [Tooltip("战斗 HUD 显示的角色名")]
        public string displayName;

        [Tooltip("角色资源目录 (Resources 相对路径)：动画帧数据 + actions / skills / spawns / buffs.json")]
        public string resourceDir;

        [Tooltip("战斗 HUD 头像")]
        public Sprite headshot;
    }

    /// <summary>
    /// 一名玩家的出战配置 (角色 + 佩戴的通灵)
    /// </summary>
    [Serializable]
    public class PlayerLoadout
    {
        [Tooltip("角色 ID (对应 CharacterTable)")]
        public int characterId = 90001;

        [Tooltip("佩戴的通灵 id (通灵系统尚未实现：目前只按数量显示通灵键上的豆)")]
        public string[] summons = Array.Empty<string>();
    }
}
