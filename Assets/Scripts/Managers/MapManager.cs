using KiHan.Config;
using KiHan.View.Render;
using UnityEngine;

/// <summary>
/// 地图管理器：开局加载地图 (背景板 + 边界逻辑)，训练场局内切换背景板
/// </summary>
public class MapManager : UnitySingleton<MapManager>
{
    public MapLogic CurrentMapLogic { get; private set; }

    /// <summary>
    /// 当前显示的地图 (未加载时为 null)
    /// </summary>
    public MapConfig CurrentMap { get; private set; }

    private GameObject _mapInstance;

    /// <summary>
    /// 开局加载：显示背景板并创建边界逻辑
    /// </summary>
    public void LoadMap(MapConfig map)
    {
        ShowScene(map);
        CurrentMapLogic = new MapLogic();
    }

    /// <summary>
    /// 局内切换地图 (训练场选图)：只替换背景板。
    /// 全部地图共用 MapLogic 边界，逻辑数据局内不变，不影响帧同步；地图若有各自的边界，需改为开局前选择
    /// </summary>
    public void ChangeScene(MapConfig map)
    {
        if (map == null || map == CurrentMap) return;
        ShowScene(map);
    }

    public void ClearMap()
    {
        DestroyScene();
        CurrentMap = null;
        CurrentMapLogic = null;
    }

    private void ShowScene(MapConfig map)
    {
        DestroyScene();
        CurrentMap = map;
        if (map == null || map.scene == null)
        {
            Debug.LogError($"[MapManager] 地图未配置场景预制体: {(map != null ? map.id : "null")}");
            return;
        }

        // 地图为 2D 背景板：放到所有角色之后 (世界 Z = BackdropDistance)，按 MapScale 放大 (见 BattleViewSpace.MapScale)
        _mapInstance = Instantiate(map.scene, new Vector3(0f, 0f, BattleViewSpace.BackdropDistance), Quaternion.identity);
        _mapInstance.transform.localScale = new Vector3(BattleViewSpace.MapScale, BattleViewSpace.MapScale, 1f);

        // 背景板不写深度，必须在透明队列中最先绘制，否则会盖住同样不写深度的特效与阴影
        foreach (SpriteRenderer sr in _mapInstance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingOrder += BattleViewSpace.BackdropSortingOffset;
        }
    }

    private void DestroyScene()
    {
        if (_mapInstance == null) return;
        Destroy(_mapInstance);
        _mapInstance = null;
    }
}
