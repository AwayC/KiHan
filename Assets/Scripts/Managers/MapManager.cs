using UnityEngine;
using Managers;
using KiHan.View.Render;

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

        // 地图为 2D 背景板：放到所有角色之后 (世界 Z = BackdropDistance)，屏幕布局与原 2D 版本一致
        _mapInstance = ResManager.Instance.Spawn(mapPath,
            new Vector3(0f, 0f, BattleViewSpace.BackdropDistance),
            Quaternion.identity);

        // 背景板不写深度，必须在透明队列中最先绘制，否则会盖住同样不写深度的特效与阴影
        foreach (SpriteRenderer sr in _mapInstance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingOrder += BattleViewSpace.BackdropSortingOffset;
        }

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
