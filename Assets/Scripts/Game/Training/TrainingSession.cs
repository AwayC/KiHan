using KiHan.Logic.AI;
using KiHan.Math;
using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 单机训练场：训练面板的开关与对敌人 (P2) 的操控。只在单机战斗中存在 (BattleManager.Training)
    /// - 敌人输入：虚拟网络打包 P2 输入帧时调用 FillDummyInput，合并面板上按住的按键与人机 (敌人攻击) 的输出
    /// - 对逻辑状态的修改 (无限火力、禁止回复、瞬移) 只在逻辑帧之间进行：BattleManager 在 World.Tick 前后调用 BeforeTick / AfterTick
    /// 单机只有一个客户端，帧之间改状态不影响确定性；联机不创建训练场
    /// </summary>
    public sealed class TrainingSession
    {
        private readonly CharacterObj _player;
        private readonly CharacterObj _dummy;
        private readonly HpMode _baseHpMode;
        private readonly IBotBrain _brain;
        private readonly InputFrame _brainOutput = new InputFrame();
        private bool _teleportPending;
        private bool _autoAttack;
        private bool _noRestore;
        private bool _damageStats;

        public TrainingSession(CharacterObj player, CharacterObj dummy, HpMode baseHpMode, IBotBrain brain)
        {
            _player = player;
            _dummy = dummy;
            _baseHpMode = baseHpMode;
            _brain = brain;
        }

        public CharacterObj Player => _player;
        public CharacterObj Dummy => _dummy;

        /// <summary>
        /// 伤害统计 (对敌人造成的伤害；只在开启统计时记录)
        /// </summary>
        public DamageStats Stats { get; } = new DamageStats();

        /// <summary>
        /// 面板上按住的敌人按键 (与人机输出合并)
        /// </summary>
        public ButtonMask DummyButtons { get; set; }

        /// <summary>
        /// 无限火力：玩家技能无 CD、查克拉常满
        /// </summary>
        public bool UnlimitedPower { get; set; }

        /// <summary>
        /// 无限暴击：玩家 (含其生成物) 每次命中必定暴击
        /// </summary>
        public bool AlwaysCrit
        {
            get => _player != null && _player.alwaysCrit;
            set
            {
                if (_player != null) _player.alwaysCrit = value;
            }
        }

        /// <summary>
        /// 伤害统计：开启时从 0 开始记录
        /// </summary>
        public bool DamageStatsEnabled
        {
            get => _damageStats;
            set
            {
                if (_damageStats == value) return;
                _damageStats = value;
                Stats.Clear();
            }
        }

        /// <summary>
        /// 敌人攻击：由人机控制敌人
        /// </summary>
        public bool AutoAttack
        {
            get => _autoAttack;
            set
            {
                if (_autoAttack == value) return;
                _autoAttack = value;
                if (value) _brain?.Reset();
            }
        }

        /// <summary>
        /// 禁止回复：双方生命值归零后停在 0 不回满，但仍可行动、可被命中；关闭后恢复开局规则 (已归零的立即回满)
        /// </summary>
        public bool NoRestore
        {
            get => _noRestore;
            set
            {
                if (_noRestore == value) return;
                _noRestore = value;
                HpMode mode = value ? HpMode.Immortal : _baseHpMode;
                _player?.SetHpMode(mode);
                _dummy?.SetHpMode(mode);
            }
        }

        /// <summary>
        /// 下一个逻辑帧开始前把敌人瞬移到玩家位置
        /// </summary>
        public void RequestTeleportDummy()
        {
            _teleportPending = true;
        }

        /// <summary>
        /// 生成敌人本帧输入 (虚拟网络打包 P2 输入帧时调用)
        /// </summary>
        public void FillDummyInput(uint frameId, InputFrame frame)
        {
            ButtonMask buttons = DummyButtons;
            byte dir = JoystickDir.None;
            if (_autoAttack && _brain != null && _dummy != null)
            {
                _brain.Think(_dummy, _player, frameId, _brainOutput);
                buttons |= _brainOutput.Buttons;
                dir = _brainOutput.JoyStickDir;
            }
            frame.Buttons = buttons;
            frame.JoyStickDir = dir;
        }

        /// <summary>
        /// 逻辑帧之前：执行瞬移请求。返回是否瞬移 (表现层需要把敌人视图直接对齐到新位置，不做插值)
        /// </summary>
        public bool BeforeTick()
        {
            if (!_teleportPending) return false;
            _teleportPending = false;
            if (_dummy == null || _player == null) return false;

            // 解除抓取 / 被抓 / 外力 / 吸附，落到玩家脚下 (玩家在空中时落在其正下方的地面)
            _dummy.ReleaseGrab();
            if (_dummy.GrabbedBy != null) _dummy.GrabbedBy.ReleaseGrab();
            _dummy.ClearForceMove();
            _dummy.StopSuction();
            FPVector3 pos = _player.motion.Position;
            pos.z = FP.Zero;
            _dummy.motion.Reset(pos);
            return true;
        }

        /// <summary>
        /// 逻辑帧之后：无限火力补满、伤害统计
        /// </summary>
        public void AfterTick(BattleWorld world)
        {
            // 在帧末补满：本帧释放技能产生的 CD 在界面读取前就被清掉，按键不会闪一下 CD
            if (UnlimitedPower && _player != null) _player.Skills?.Refill();

            if (!_damageStats || _dummy == null) return;
            for (int i = 0; i < world.HitEvents.Count; i++)
            {
                HitEvent hit = world.HitEvents[i];
                if (hit.TargetId == _dummy.EntityId) Stats.Record(hit.FrameId, hit.Damage);
            }
            Stats.Tick(world.FrameId);
        }
    }
}
