using UnityEngine;

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
        
        // 初始化地图边界逻辑
        // 实际开发中，这些数据可以从地图预制体的脚本中获取，或者从配置表读取
        CurrentMapLogic = new MapLogic();
        
        // 更新相机边界
        if (CameraControllor.Instance != null)
        {
            CameraControllor.Instance.SetBoundaries(CurrentMapLogic.MinX + 5f, CurrentMapLogic.MaxX - 5f);
        }
    }
}
