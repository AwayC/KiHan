using System;
using System.Collections.Generic;
using KiHan.Asset;
using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 地图注册表：游戏可用的地图 (Resources/Config/MapRegistry.asset)
    /// 插拔地图 = 在这里增删一行或切换启用；登记顺序即选图列表顺序，第一张已启用的地图为开局默认地图
    /// </summary>
    [CreateAssetMenu(fileName = "MapRegistry", menuName = "KiHan/Map Registry")]
    public class MapRegistry : ScriptableObject
    {
        public const string ResourcePath = "Config/MapRegistry";

        public List<MapRegistryEntry> maps = new List<MapRegistryEntry>();

        private static MapRegistry _instance;

        /// <summary>
        /// 首次访问时加载 (注册表常驻)
        /// </summary>
        public static MapRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetManager.Instance.Load<MapRegistry>(ResourcePath);
                    if (_instance == null) Debug.LogError($"[MapRegistry] 找不到地图注册表: {ResourcePath}");
                }
                return _instance;
            }
        }

        /// <summary>
        /// 开局默认地图：第一张已启用的地图，没有返回 null
        /// </summary>
        public static MapConfig Default
        {
            get
            {
                MapRegistry registry = Instance;
                if (registry == null) return null;
                foreach (MapRegistryEntry entry in registry.maps)
                {
                    if (entry.enabled && entry.map != null) return entry.map;
                }
                return null;
            }
        }

        /// <summary>
        /// 按登记顺序把已启用的地图追加到 result
        /// </summary>
        public static void CollectEnabled(List<MapConfig> result)
        {
            MapRegistry registry = Instance;
            if (registry == null) return;
            foreach (MapRegistryEntry entry in registry.maps)
            {
                if (entry.enabled && entry.map != null) result.Add(entry.map);
            }
        }
    }

    [Serializable]
    public class MapRegistryEntry
    {
        [Tooltip("关闭后该地图不出现在选图列表 (保留登记，便于临时拔出)")]
        public bool enabled = true;

        public MapConfig map;
    }
}
