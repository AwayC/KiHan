using KiHan.Config;
using KiHan.Logic;
using Managers;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI
{
    /// <summary>
    /// 战斗 HUD (预制体 UI/Button/Canvas 根节点)：节点全部在预制体中序列化引用，不按名字查找
    /// - 顶部：双方角色状态 (按槽位 P1 / P2)
    /// - 本机按键：技能 CD / 接段、替身可用次数、通灵数量；按键输入由各按键上的 VirtualButton / VirtualJoystick 直接写入 InputManager
    /// - 返回确认弹窗
    /// 开局由 BattleManager 调用 Bind；之后每帧只读角色状态，各视图只在数值变化时修改 UI
    /// </summary>
    public class BattleHudPanel : BasePanel
    {
        [Header("顶部状态")]
        [SerializeField] private FighterStatusView _p1Status;
        [SerializeField] private FighterStatusView _p2Status;

        [Header("本机按键")]
        [SerializeField] private SkillButtonView[] _skillButtons;
        [SerializeField] private SubstitutionButtonView _substitution;
        [SerializeField] private SummonButtonView _summon;

        [Header("返回")]
        [SerializeField] private Button _backButton;
        [SerializeField] private GameObject _backPanel;
        [SerializeField] private Button _backClose;
        [SerializeField] private Button _backCancel;
        [SerializeField] private Button _backConfirm;

        /// <summary>
        /// 本机角色的技能图标 (绑定时按 skills.json 预载)
        /// </summary>
        private readonly SkillIconCache _icons = new SkillIconCache();

        private CharacterObj _local;

        private void Awake()
        {
            _backPanel.SetActive(false);
            _backButton.onClick.AddListener(() => UIPanelAnim.Show(this, _backPanel));
            _backClose.onClick.AddListener(HideBackPanel);
            _backCancel.onClick.AddListener(HideBackPanel);
            _backConfirm.onClick.AddListener(() =>
            {
                HideBackPanel();
                GameApp.Instance.ExitGame();
            });
        }

        private void HideBackPanel() => UIPanelAnim.Hide(this, _backPanel);

        /// <param name="p1">槽位 1 的角色</param>
        /// <param name="p2">槽位 2 的角色</param>
        /// <param name="local">本机玩家控制的角色 (按键显示其技能)</param>
        /// <param name="localLoadout">本机玩家的出战配置 (通灵数量)</param>
        public void Bind(CharacterObj p1, CharacterObj p2, CharacterObj local, PlayerLoadout localLoadout)
        {
            _p1Status.Bind(p1);
            _p2Status.Bind(p2);

            _local = local;
            _icons.Clear();
            if (local != null && local.Skills != null) _icons.Preload(local.Skills);
            foreach (SkillButtonView button in _skillButtons) button.Bind(_icons);

            // 通灵系统尚未实现：佩戴的通灵都还没有使用
            int summons = localLoadout != null && localLoadout.summons != null ? localLoadout.summons.Length : 0;
            _summon.Show(summons, summons);

            Refresh();
        }

        private void Update()
        {
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
