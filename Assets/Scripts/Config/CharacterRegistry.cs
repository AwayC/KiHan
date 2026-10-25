using System;
using System.Collections.Generic;
using KiHan.Asset;
using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 角色注册表：游戏可用的角色包 (Resources/Config/CharacterRegistry.asset)
    /// 插拔角色 = 在这里增删一行或切换启用；角色包本身放在工程任意位置 (清单直接引用资源，随包一起打进游戏)
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterRegistry", menuName = "KiHan/Character Registry")]
    public class CharacterRegistry : ScriptableObject
    {
        public const string ResourcePath = "Config/CharacterRegistry";

        public List<CharacterRegistryEntry> characters = new List<CharacterRegistryEntry>();

        private static CharacterRegistry _instance;

        /// <summary>
        /// 首次访问时加载 (注册表常驻)
        /// </summary>
        public static CharacterRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetManager.Instance.Load<CharacterRegistry>(ResourcePath);
                    if (_instance == null) Debug.LogError($"[CharacterRegistry] 找不到角色注册表: Resources/{ResourcePath}");
                }
                return _instance;
            }
        }

        /// <summary>
        /// 查找已启用的角色包，没有返回 null
        /// </summary>
        public static CharacterManifest Find(int id)
        {
            CharacterRegistry registry = Instance;
            if (registry == null) return null;
            foreach (CharacterRegistryEntry entry in registry.characters)
            {
                if (entry.enabled && entry.manifest != null && entry.manifest.id == id) return entry.manifest;
            }
            return null;
        }
    }

    [Serializable]
    public class CharacterRegistryEntry
    {
        [Tooltip("关闭后该角色不可用 (保留登记，便于临时拔出)")]
        public bool enabled = true;

        public CharacterManifest manifest;
    }
}
