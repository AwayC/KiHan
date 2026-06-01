using UnityEngine;
using Managers;

/// <summary>
/// 地图管理器，负责加载地图和提供边界信息
/// </summary>
public class MapManager : UnitySingleton<MapManager>
{
    public MapLogic CurrentMapLogic { get; private set; }
    private GameObject _mapInstance;

    public void LoadMap(string mapPath)
    {
        if (_mapInstance != null)
        {
            Destroy(_mapInstance);
        }

        // 简单实例化地图
        _mapInstance = ResManager.Instance.Spawn(mapPath, Vector3.zero, Quaternion.identity);
        
        // 移除之前的“拍扁”逻辑，保留预制体原有的层级关系
        // 只要预制体里最高的层级小于玩家的 10 即可正常显示

        // 初始化地图边界逻辑
        CurrentMapLogic = new MapLogic();
    }

    public void ClearMap()
    {
        if (_mapInstance != null)
        {
            Destroy(_mapInstance);
            _mapInstance = null;
        }
        CurrentMapLogic = null;
    }
}
