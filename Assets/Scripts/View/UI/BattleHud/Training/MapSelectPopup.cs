using System;
using System.Collections.Generic;
using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 训练场选图弹窗 (战斗 HUD 的场景按钮打开)：左右滑动列出地图注册表中已启用的地图，
    /// 选择后局内切换背景板并关闭 (选当前地图只关闭)；点击空白处关闭
    /// </summary>
    [UIPanel("UI/Panels/BattleHud/Training/MapSelectPopup", UILayer.Popup)]
    public partial class MapSelectPopup : UIPanel
    {
        private readonly List<MapConfig> _maps = new List<MapConfig>();
        private Action<MapItemView, int> _bindItem;
        private Action<int> _onSelect;

        protected override void OnCreate()
        {
            _bindItem = BindItem;
            _onSelect = OnSelect;
            _closeBtn.onClick.AddListener(Close);
        }

        protected override void OnOpen()
        {
            _maps.Clear();
            MapRegistry.CollectEnabled(_maps);
            _mapList.SetCount(_maps.Count, _bindItem);

            int current = _maps.IndexOf(MapManager.Instance.CurrentMap);
            if (current >= 0) _mapList.ScrollTo(current, false);
        }

        private void BindItem(MapItemView view, int index)
        {
            view.Bind(_maps[index], index, _onSelect);
        }

        private void OnSelect(int index)
        {
            MapManager.Instance.ChangeScene(_maps[index]);
            Close();
        }
    }
}
