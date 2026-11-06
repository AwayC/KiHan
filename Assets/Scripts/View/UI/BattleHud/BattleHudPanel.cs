using System;
using KiHan.Config;
using KiHan.Logic;
using View.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 战斗 HUD (Hud 层)：GameFlow 在战斗加载完成后带参数打开
    /// - 顶部：双方角色状态 (按槽位 P1 / P2)
    /// - 本机按键：技能 / 秘卷 CD 与接段、Buff 倒计时 (buffs 的 button 配置在普攻等按键上)、替身可用次数、通灵数量；按键输入由各按键上的 VirtualButton / VirtualJoystick 直接写入 InputManager
    ///   (秘卷键与通灵键的父节点是底图，图标在子节点 @SecretBtn / @SummonBtn 上；秘卷图标与 CD 由 @SecretBtn 上的 SkillButtonView 显示)
    /// - 返回：通用确认弹窗，确认后 GameFlow.ExitBattle
    /// - 场景：打开训练场选图弹窗 (MapSelectPopup)
    /// - 换人：角色状态上的换人按钮打开训练场换人弹窗 (CharSelectPopup)，联机时隐藏
    /// - 设置：单机训练场设置 (TrainingSettingView，嵌在 HUD 中，联机时隐藏)
    /// 之后每帧只读角色状态，各部件只在数值变化时修改 UI
    /// </summary>
    [UIPanel("UI/Panels/BattleHud/BattleHudPanel", UILayer.Hud)]
    public partial class BattleHudPanel : UIPanel<BattleHudPanel.Args>
    {
        public struct Args
        {
            /// <summary>槽位 1 的角色</summary>
            public CharacterObj P1;

            /// <summary>槽位 2 的角色</summary>
            public CharacterObj P2;

            /// <summary>本机玩家控制的角色 (按键显示其技能)</summary>
            public CharacterObj Local;

            /// <summary>本机玩家的出战配置 (通灵数量)</summary>
            public PlayerLoadout LocalLoadout;

            /// <summary>单机训练场 (联机为 null，隐藏设置按钮与训练面板)</summary>
            public TrainingSession Training;
        }

        private const string ExitConfirmText = "确定退出战斗吗？";

        /// <summary>
        /// 本机角色的技能图标 (打开时按 skills.json 预载)
        /// </summary>
        private readonly SkillIconCache _icons = new SkillIconCache();

        private CharacterObj _local;
        private Action<CharacterObj> _openCharSelect;

        protected override void OnCreate()
        {
            _openCharSelect = OpenCharSelect;
            _backButton.onClick.AddListener(() => UIManager.Instance.Confirm(ExitConfirmText, GameFlow.Instance.ExitBattle));
            _mapSelectBtn.onClick.AddListener(() => UIManager.Instance.Open<MapSelectPopup>());
            _settingButton.onClick.AddListener(_trainingSettingPanel.ToggleSettings);
        }

        protected override void OnOpen(Args args)
        {
            Action<CharacterObj> changeCharacter = args.Training != null ? _openCharSelect : null;
            _p1Status.Bind(args.P1, changeCharacter);
            _p2Status.Bind(args.P2, changeCharacter);

            _local = args.Local;
            _icons.Clear();
            SecretScrollEntry scroll = args.LocalLoadout != null ? SecretScrollRegistry.Find(args.LocalLoadout.secret) : null;
            if (scroll != null) _icons.Register(scroll.IconKey, scroll.icon);
            if (_local != null && _local.Skills != null) _icons.Preload(_local.Skills, CharacterRegistry.Find(_local.CharacterId));
            foreach (SkillButtonView button in _skillButtons) button.Bind(_icons);
            _secretBtn.Bind(_icons);

            // 通灵系统尚未实现：佩戴的通灵都还没有使用
            int summons = args.LocalLoadout != null && args.LocalLoadout.summons != null ? args.LocalLoadout.summons.Length : 0;
            _summon.Show(summons, summons);

            _settingButton.gameObject.SetActive(args.Training != null);
            _trainingSettingPanel.Bind(args.Training);

            Refresh();
        }

        protected override void OnClose()
        {
            _local = null;
            _icons.Clear();
            _trainingSettingPanel.Bind(null);
        }

        private void Update()
        {
            if (!IsOpen) return;
            Refresh();
        }

        private static void OpenCharSelect(CharacterObj character)
        {
            UIManager.Instance.Open<CharSelectPopup, CharSelectPopup.Args>(new CharSelectPopup.Args { Slot = character.InputSlot, CurrentCharacterId = character.CharacterId, CurrentSecret = BattleManager.Instance.SecretOf(character.InputSlot) });
        }

        private void Refresh()
        {
            _p1Status.Refresh();
            _p2Status.Refresh();

            if (_local == null) return;
            _attackTimer.Refresh(_local.Buffs);

            SkillController skills = _local.Skills;
            if (skills == null) return;
            for (int i = 0; i < _skillButtons.Length; i++) _skillButtons[i].Refresh(skills, _local.Buffs);
            _secretBtn.Refresh(skills, _local.Buffs);
            _substitution.Refresh(skills);
        }
    }
}
