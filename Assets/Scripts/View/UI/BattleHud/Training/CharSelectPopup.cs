using System;
using System.Collections.Generic;
using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 训练场换人弹窗 (战斗 HUD 角色状态上的换人按钮打开)：上下滑动的网格列出角色注册表中已启用的忍者，
    /// 打开时选中该槽位当前的忍者 (选中框呼吸灯)；点卡片切换选中，确定后重开训练场 (选当前忍者只关闭)；点击空白处关闭
    /// </summary>
    [UIPanel("UI/Panels/BattleHud/Training/CharSelectPopup", UILayer.Popup)]
    public partial class CharSelectPopup : UIPanel<CharSelectPopup.Args>
    {
        public struct Args
        {
            /// <summary>要换人的槽位 (1 = P1 玩家，2 = P2 敌人)</summary>
            public int Slot;

            /// <summary>该槽位当前的角色 ID</summary>
            public int CurrentCharacterId;
        }

        private readonly List<CharacterManifest> _characters = new List<CharacterManifest>();
        private Action<CharSelectItemView, int> _bindItem;
        private Action<int> _onSelect;
        private int _slot;
        private int _currentIndex;
        private int _selectedIndex;

        protected override void OnCreate()
        {
            _bindItem = BindItem;
            _onSelect = OnSelect;
            _closeBtn.onClick.AddListener(Close);
            _confirmBtn.onClick.AddListener(OnConfirm);
        }

        protected override void OnOpen(Args args)
        {
            _slot = args.Slot;
            _characters.Clear();
            CharacterRegistry.CollectEnabled(_characters);
            _currentIndex = _characters.FindIndex(m => m.id == args.CurrentCharacterId);
            _selectedIndex = _currentIndex;

            _list.SetCount(_characters.Count, _bindItem);
            if (_selectedIndex >= 0) _list.ScrollTo(_selectedIndex, false);
        }

        private void BindItem(CharSelectItemView view, int index)
        {
            view.Bind(_characters[index], index, index == _selectedIndex, _onSelect);
        }

        private void OnSelect(int index)
        {
            if (index == _selectedIndex) return;
            _selectedIndex = index;
            _list.RefreshItems();
        }

        private void OnConfirm()
        {
            int characterId = _selectedIndex >= 0 && _selectedIndex != _currentIndex ? _characters[_selectedIndex].id : 0;
            Close();
            if (characterId != 0) GameFlow.Instance.RestartTraining(_slot, characterId);
        }
    }
}
