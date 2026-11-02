using System;
using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 选图列表的一项：地图截图、地图名、选择按钮
    /// </summary>
    public partial class MapItemView : UIView
    {
        private Action<int> _onSelect;
        private int _index;

        private void Awake()
        {
            _selectBtn.onClick.AddListener(OnSelectClicked);
        }

        public void Bind(MapConfig map, int index, Action<int> onSelect)
        {
            _index = index;
            _onSelect = onSelect;
            _shot.sprite = map.shot;
            _mapName.text = map.displayName;
        }

        private void OnSelectClicked()
        {
            _onSelect?.Invoke(_index);
        }
    }
}
