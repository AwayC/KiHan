using KiHan.Logic;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 训练场设置 (嵌在战斗 HUD 中，单机才显示；非模态，不挡没被覆盖的 HUD 按键)：
    /// - 第一行开关 (开启显示原色，关闭压暗)：伤害统计 (显示数据面板)、敌人攻击 (人机)、无限火力、无限暴击、禁止回复
    /// - 第二、三行：按住即敌人 (P2) 按住对应按键，长按 DummyKeyLatchSeconds 秒后松手保持按住 (常亮)，再点一次解除；改变敌人位置 = 敌人瞬移到玩家脚下
    /// - 数据面板：总伤害、命中次数、总时间、秒伤，只在命中 / 清零时刷新 (6 秒无伤害清零，见 DamageStats)
    /// 设置区由 HUD 的设置按钮开关、关闭按钮收起；数据面板独立于设置区，随"伤害统计"显示
    /// </summary>
    public partial class TrainingSettingView : UIView
    {
        private TrainingSession _session;
        private int _statsVersion = -1;

        /// <summary>
        /// 敌人按键长按锁定所需秒数
        /// </summary>
        private const float DummyKeyLatchSeconds = 2f;

        private void Awake()
        {
            _demageCountBtn.Changed += OnDamageStatsChanged;
            _autoAttackBtn.Changed += on => { if (_session != null) _session.AutoAttack = on; };
            _unlimitedPowerBtn.Changed += on => { if (_session != null) _session.UnlimitedPower = on; };
            _closeRestoreBtn.Changed += on => { if (_session != null) _session.NoRestore = on; };
            _closeCriticalHitBtn.Changed += on => { if (_session != null) _session.AlwaysCrit = on; };

            BindDummyKey(_attackBtn, ButtonMask.Attack);
            BindDummyKey(_skill1Btn, ButtonMask.Skill1);
            BindDummyKey(_skill2Btn, ButtonMask.Skill2);
            BindDummyKey(_skill3Btn, ButtonMask.Ultimate);
            BindDummyKey(_summonBtn, ButtonMask.Summon);
            BindDummyKey(_secretBtn, ButtonMask.Secret);
            BindDummyKey(_substitutionBtn, ButtonMask.Substitution);

            _changePosBtn.onClick.AddListener(() => _session?.RequestTeleportDummy());
            _closeBtn.onClick.AddListener(() => _settings.SetActive(false));
        }

        /// <summary>
        /// 绑定训练场 (战斗 HUD 打开时)；session 为 null (联机 / 关闭 HUD) 时整体隐藏
        /// </summary>
        public void Bind(TrainingSession session)
        {
            _session = session;
            gameObject.SetActive(session != null);
            if (session == null) return;

            _settings.SetActive(false);
            _demageCountBtn.SetOn(session.DamageStatsEnabled);
            _autoAttackBtn.SetOn(session.AutoAttack);
            _unlimitedPowerBtn.SetOn(session.UnlimitedPower);
            _closeCriticalHitBtn.SetOn(session.AlwaysCrit);
            _closeRestoreBtn.SetOn(session.NoRestore);
            _dataPanel.SetActive(session.DamageStatsEnabled);
            _statsVersion = -1;
            RefreshStats();
        }

        /// <summary>
        /// 显示 / 收起设置区 (HUD 设置按钮)
        /// </summary>
        public void ToggleSettings()
        {
            if (_session != null) _settings.SetActive(!_settings.activeSelf);
        }

        private void Update()
        {
            if (_session == null || !_dataPanel.activeSelf) return;
            if (_session.Stats.Version != _statsVersion) RefreshStats();
        }

        private void OnDamageStatsChanged(bool on)
        {
            if (_session == null) return;
            _session.DamageStatsEnabled = on;
            _dataPanel.SetActive(on);
            RefreshStats();
        }

        private void BindDummyKey(UIHoldButton key, ButtonMask mask)
        {
            key.LatchSeconds = DummyKeyLatchSeconds;
            key.PressChanged += pressed =>
            {
                if (_session == null) return;
                _session.DummyButtons = pressed ? _session.DummyButtons | mask : _session.DummyButtons & ~mask;
            };
        }

        private void RefreshStats()
        {
            if (_session == null) return;
            DamageStats stats = _session.Stats;
            _statsVersion = stats.Version;
            _totalDemageText.SetText("{0:0}", stats.TotalDamage.AsFloat());
            _hitCountText.SetText("{0}", stats.HitCount);
            _totalTimeText.SetText("{0:1}s", stats.Seconds.AsFloat());
            _demagePerSecText.SetText("{0:0}", stats.DamagePerSecond.AsFloat());
        }
    }
}
