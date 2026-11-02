using KiHan.Config;
using KiHan.Logic;
using View.UI;

namespace KiHan.UI
{
    /// <summary>
    /// 战斗 HUD (Hud 层)：GameFlow 在战斗加载完成后带参数打开
    /// - 顶部：双方角色状态 (按槽位 P1 / P2)
    /// - 本机按键：技能 CD / 接段、替身可用次数、通灵数量；按键输入由各按键上的 VirtualButton / VirtualJoystick 直接写入 InputManager
    /// - 返回：通用确认弹窗，确认后 GameFlow.ExitBattle
    /// - 场景：打开训练场选图弹窗 (MapSelectPopup)
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
        }

        private const string ExitConfirmText = "确定退出战斗吗？";

        /// <summary>
        /// 本机角色的技能图标 (打开时按 skills.json 预载)
        /// </summary>
        private readonly SkillIconCache _icons = new SkillIconCache();

        private CharacterObj _local;

        protected override void OnCreate()
        {
            _backButton.onClick.AddListener(() => UIManager.Instance.Confirm(ExitConfirmText, GameFlow.Instance.ExitBattle));
            _mapSelectBtn.onClick.AddListener(() => UIManager.Instance.Open<MapSelectPopup>());
        }

        protected override void OnOpen(Args args)
        {
            _p1Status.Bind(args.P1);
            _p2Status.Bind(args.P2);

            _local = args.Local;
            _icons.Clear();
            if (_local != null && _local.Skills != null) _icons.Preload(_local.Skills, CharacterRegistry.Find(_local.CharacterId));
            foreach (SkillButtonView button in _skillButtons) button.Bind(_icons);

            // 通灵系统尚未实现：佩戴的通灵都还没有使用
            int summons = args.LocalLoadout != null && args.LocalLoadout.summons != null ? args.LocalLoadout.summons.Length : 0;
            _summon.Show(summons, summons);

            Refresh();
        }

        protected override void OnClose()
        {
            _local = null;
            _icons.Clear();
        }

        private void Update()
        {
            if (!IsOpen) return;
            Refresh();
        }

        private void Refresh()
        {
            _p1Status.Refresh();
            _p2Status.Refresh();

            SkillController skills = _local != null ? _local.Skills : null;
            if (skills == null) return;
            for (int i = 0; i < _skillButtons.Length; i++) _skillButtons[i].Refresh(skills);
            _substitution.Refresh(skills);
        }
    }
}
