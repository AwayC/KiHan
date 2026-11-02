using UnityEngine;

namespace KiHan.Config
{
    /// <summary>
    /// 地图清单：一个文件夹就是一张地图 (Assets/AssetPackages/Maps/&lt;id&gt;/Map.asset)，选图截图 shot.png 放同目录
    /// 清单直接引用场景背景板预制体与截图，登记到地图注册表 (MapRegistry) 后即可在选图界面使用
    /// 全部地图共用 MapLogic 边界 (见 MapLogic)，地图只决定背景画面
    /// </summary>
    [CreateAssetMenu(fileName = "Map", menuName = "KiHan/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [Tooltip("地图 ID，全局唯一 (与目录名一致，如 01)")]
        public string id;

        [Tooltip("选图界面显示的地图名")]
        public string displayName;

        [Tooltip("场景背景板预制体 (MapManager 实例化)")]
        public GameObject scene;

        [Tooltip("选图界面的地图截图")]
        public Sprite shot;
    }
}
