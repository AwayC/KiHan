using System;
using System.Collections.Generic;
using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 训练场换人弹窗 (战斗 HUD 角色状态上的换人按钮打开)：上方页签切换忍者页 / 秘卷页，各页是上下滑动的网格
    /// - 忍者页列出角色注册表中已启用的忍者，秘卷页列出秘卷表中的秘卷；打开时选中该槽位当前的忍者与秘卷 (选中框呼吸灯)
    /// - 点卡片切换选中；再点已选中的秘卷取消佩戴
    /// - 确定后用选中的忍者 + 秘卷重开训练场 (都没变只关闭)；点击空白处关闭
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

            /// <summary>该槽位当前佩戴的秘卷 ID (没有为 null)</summary>
            public string CurrentSecret;
        }

        private enum Page
        {
            Character,
            Secret,
        }

        private readonly List<CharacterManifest> _characters = new List<CharacterManifest>();
        private readonly List<SecretScrollEntry> _scrolls = new List<SecretScrollEntry>();
        private Action<CharSelectItemView, int> _bindCharacter;
        private Action<SecretSelectItemView, int> _bindScroll;
        private Action<int> _onSelectCharacter;
        private Action<int> _onSelectScroll;
        private int _slot;
        private int _currentCharacterId;
        private string _currentSecret;
        private int _characterIndex;
        private int _scrollIndex;

        protected override void OnCreate()
        {
            _bindCharacter = BindCharacter;
            _bindScroll = BindScroll;
            _onSelectCharacter = OnSelectCharacter;
            _onSelectScroll = OnSelectScroll;
            _closeBtn.onClick.AddListener(Close);
            _confirmBtn.onClick.AddListener(OnConfirm);
            _charPage.Clicked += () => ShowPage(Page.Character);
            _secretPage.Clicked += () => ShowPage(Page.Secret);
        }

        protected override void OnOpen(Args args)
        {
            _slot = args.Slot;
            _currentCharacterId = args.CurrentCharacterId;
            _currentSecret = string.IsNullOrEmpty(args.CurrentSecret) ? null : args.CurrentSecret;

            _characters.Clear();
            CharacterRegistry.CollectEnabled(_characters);
            _characterIndex = _characters.FindIndex(m => m.id == _currentCharacterId);

            _scrolls.Clear();
            SecretScrollRegistry registry = SecretScrollRegistry.Instance;
            if (registry != null) _scrolls.AddRange(registry.scrolls);
            _scrollIndex = _currentSecret != null ? _scrolls.FindIndex(s => s.id == _currentSecret) : -1;

            ShowPage(Page.Character);
            _charList.SetCount(_characters.Count, _bindCharacter);
            if (_characterIndex >= 0) _charList.ScrollTo(_characterIndex, false);
            _secretList.SetCount(_scrolls.Count, _bindScroll);
            if (_scrollIndex >= 0) _secretList.ScrollTo(_scrollIndex, false);
        }

        private void ShowPage(Page page)
        {
            bool character = page == Page.Character;
            _charPage.SetSelected(character);
            _secretPage.SetSelected(!character);
            if (_charList.gameObject.activeSelf != character) _charList.gameObject.SetActive(character);
            if (_secretList.gameObject.activeSelf == character) _secretList.gameObject.SetActive(!character);
        }

        private void BindCharacter(CharSelectItemView view, int index)
        {
            view.Bind(_characters[index], index, index == _characterIndex, _onSelectCharacter);
        }

        private void BindScroll(SecretSelectItemView view, int index)
        {
            view.Bind(_scrolls[index], index, index == _scrollIndex, _onSelectScroll);
        }

        private void OnSelectCharacter(int index)
        {
            if (index == _characterIndex) return;
            _characterIndex = index;
            _charList.RefreshItems();
        }

        private void OnSelectScroll(int index)
        {
            _scrollIndex = index == _scrollIndex ? -1 : index;
            _secretList.RefreshItems();
        }

        private void OnConfirm()
        {
            int characterId = _characterIndex >= 0 ? _characters[_characterIndex].id : _currentCharacterId;
            string secret = _scrollIndex >= 0 ? _scrolls[_scrollIndex].id : null;
            Close();
            if (characterId != _currentCharacterId || secret != _currentSecret) GameFlow.Instance.RestartTraining(_slot, characterId, secret);
        }
    }
}
