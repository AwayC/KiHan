using System;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 角色动作与战斗实体逻辑主体 (纯定点数确定性运算)
    /// </summary>
    public class CharacterObj : MonoBehaviour
    {
        /// <summary>
        /// 动作控制器
        /// </summary>
        public ActionController action;

        /// <summary>
        /// 指令与输入系统
        /// </summary>
        public InputToCommand input;

        /// <summary>
        /// 角色重量 (定点数)，影响下落加速度
        /// </summary>
        public FP weight = FP.Parse("4.5");

        /// <summary>
        /// 是否处于下落状态 (由物理环境层或主逻辑统一裁决)
        /// </summary>
        private bool _falling = false;

        /// <summary>
        /// 当前下落累计重力位移量 (定点数)
        /// </summary>
        private FP _curWeight = FP.Zero;

        public bool Falling
        {
            get => _falling;
            set
            {
                if (value && !_falling)
                {
                    _curWeight = FP.Zero;
                }
                _falling = value;
            }
        }

        /// <summary>
        /// 攻击盒与受击盒当前的接触状态缓存
        /// </summary>
        private readonly Dictionary<AttackHitBox, List<BeHitBox>> _boxTouches = new Dictionary<AttackHitBox, List<BeHitBox>>();

        /// <summary>
        /// 当前动作对各目标的命中冷却记录 (换动作时重置)
        /// </summary>
        public List<HitRecord> HitRecords { get; private set; } = new List<HitRecord>();

        /// <summary>
        /// 当前施加的强制外力位移
        /// </summary>
        private ForceMove _forceMove = ForceMove.NoForceMove;

        private bool UnderForceMove => _forceMove.TimeElapsed <= _forceMove.Data.inSec;

        public bool Inversed
        {
            get => input != null && input.inversed;
            set
            {
                if (input != null) input.inversed = value;
            }
        }

        private bool WishToMoveForward =>
            input != null && (input.IsKeyHolding(CommandKey.Forward) || input.ActionOccur(new CommandKey[] { CommandKey.Forward }, validTicks: 1));

        private bool WishToMoveBackward =>
            input != null && (input.IsKeyHolding(CommandKey.Backward) || input.ActionOccur(new CommandKey[] { CommandKey.Backward }, validTicks: 1));

        /// <summary>
        /// 移动速度 (定点数，米/秒)
        /// </summary>
        public FP moveSpeed = FP.Parse("3.0");

        private void Awake()
        {
            if (action != null)
            {
                action.Set((wasAction, toAction) =>
                {
                    // 更换动作时清除所有命中记录
                    HitRecords.Clear();
                });
            }

            // 初始化子物体中的判定盒所属主人
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform trans in transforms)
            {
                AttackHitBox ahb = trans.GetComponent<AttackHitBox>();
                if (ahb != null) ahb.master = this;

                BeHitBox bhb = trans.GetComponent<BeHitBox>();
                if (bhb != null) bhb.master = this;
            }
        }

        /// <summary>
        /// 房间逻辑帧步进：默认以 GameConfig.LOGIC_TICK_TIME_FP 推进
        /// </summary>
        public void Tick() => Tick(GameConfig.LOGIC_TICK_TIME_FP);

        public void Tick(FP deltaFP)
        {
            // 推进动作状态机
            action?.Tick();

            // 推进命中冷却倒计时 (非受击停顿/卡帧时)
            if (action != null && !action.Freezing)
            {
                for (int i = 0; i < HitRecords.Count; i++)
                {
                    HitRecords[i].Tick(deltaFP);
                }
            }
        }

        /// <summary>
        /// 自然移动位移计算 (纯定点数确定性向量)
        /// </summary>
        public FPVector3 NatureMove() => NatureMove(GameConfig.LOGIC_TICK_TIME_FP);

        public FPVector3 NatureMove(FP deltaFP)
        {
            FP horizMove = FP.Zero;
            if (WishToMoveForward)
                horizMove = moveSpeed * action.MoveInputAcceptance * deltaFP;
            else if (WishToMoveBackward)
                horizMove = -moveSpeed * action.MoveInputAcceptance * deltaFP;

            FP x = (horizMove + action.RootMotionMove.x) * (Inversed ? -FP.One : FP.One);
            FP y = action.RootMotionMove.y - _curWeight;
            FP z = action.RootMotionMove.z;

            return new FPVector3(x, y, z);
        }

        public Vector3 NatureMove(int ticks)
        {
            return NatureMove(GameConfig.LOGIC_TICK_TIME_FP * ticks).ToVector3();
        }

        /// <summary>
        /// 获取当前逻辑帧的总位移增量 (纯定点数确定性运算)
        /// </summary>
        public FPVector3 ThisTickMove() => ThisTickMove(GameConfig.LOGIC_TICK_TIME_FP);

        public FPVector3 ThisTickMove(FP deltaFP)
        {
            // 卡顿/硬直时不移动
            if (action != null && action.Freezing) return FPVector3.zero;

            // 优先执行强制外力位移
            if (UnderForceMove)
            {
                FPVector3 fMove = _forceMove.MoveTween(_forceMove);
                _forceMove.StepTick(deltaFP);
                return fMove;
            }

            // 下落重力累加
            if (_falling) _curWeight += weight * deltaFP;
            else _curWeight = FP.Zero;

            // 无强制外力时执行自然移动
            return NatureMove(deltaFP);
        }


        /// <summary>
        /// 设置强制外力位移
        /// </summary>
        public void SetForceMove(MoveInfo force)
        {
            _forceMove = ForceMove.FromData(force);
        }

        /// <summary>
        /// 攻击盒子当前是否激活？
        /// </summary>
        public bool ShouldAttackBoxActive(string[] tags)
        {
            if (tags == null || action == null) return false;
            foreach (string s in tags)
            {
                if (action.ActiveAttackBoxTag.Contains(s))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 受击盒子当前是否激活？
        /// </summary>
        public bool ShouldBeHitBoxActive(string[] tags)
        {
            if (tags == null || action == null) return false;
            foreach (string s in tags)
            {
                if (action.ActiveBeHitBoxTag.Contains(s))
                    return true;
            }
            return false;
        }

        public void OnAttackBoxHit(AttackHitBox attackHitBox, BeHitBox targetBox)
        {
            if (attackHitBox == null || targetBox == null) return;

            if (!_boxTouches.ContainsKey(attackHitBox))
                _boxTouches.Add(attackHitBox, new List<BeHitBox>());

            if (!_boxTouches[attackHitBox].Contains(targetBox))
                _boxTouches[attackHitBox].Add(targetBox);
        }

        public void OnAttackBoxExit(AttackHitBox attackHitBox, BeHitBox beHitBox)
        {
            if (attackHitBox == null || beHitBox == null) return;
            if (!_boxTouches.ContainsKey(attackHitBox)) return;
            _boxTouches[attackHitBox].Remove(beHitBox);
        }

        private BeHitBoxTurnOnInfo GetDefensePhaseByBeHitBox(BeHitBox box)
        {
            if (box == null || box.tags == null || action == null) return new BeHitBoxTurnOnInfo();

            foreach (string boxTag in box.tags)
            {
                foreach (BeHitBoxTurnOnInfo info in action.ActiveBeHitBoxInfo)
                {
                    if (info.tag == null) continue;
                    foreach (string infoTag in info.tag)
                    {
                        if (infoTag == boxTag)
                            return info;
                    }
                }
            }
            return new BeHitBoxTurnOnInfo();
        }

        /// <summary>
        /// 判定当前帧是否命中目标角色 (冒泡出最高优先级的判定结果)
        /// </summary>
        public bool CanAttackTargetNow(CharacterObj target, out AttackInfo attackPhase, out BeHitBoxTurnOnInfo defensePhase, out AttackHitBox attackBox, out BeHitBox targetBox)
        {
            attackBox = null;
            targetBox = null;
            attackPhase = new AttackInfo();
            defensePhase = new BeHitBoxTurnOnInfo();

            if (target == null || target.action == null || action == null) return false;

            // 收集目标激活的受击盒 Tag
            List<string> activeBoxTag = new List<string>();
            foreach (BeHitBoxTurnOnInfo info in target.action.ActiveBeHitBoxInfo)
            {
                if (info.tag == null) continue;
                foreach (string t in info.tag)
                {
                    if (!activeBoxTag.Contains(t)) activeBoxTag.Add(t);
                }
            }

            // 遍历自身所有激活的攻击盒判定
            foreach (AttackBoxTurnOnInfo boxInfo in action.ActiveAttackBoxInfo)
            {
                foreach (KeyValuePair<AttackHitBox, List<BeHitBox>> touch in _boxTouches)
                {
                    if (!touch.Key.Active) continue;

                    BeHitBox best = null;
                    int bestPriority = 0;
                    foreach (BeHitBox hitBox in touch.Value)
                    {
                        if (hitBox == null || !hitBox.Active || hitBox.master != target || !hitBox.TagHit(activeBoxTag)) continue;

                        BeHitBoxTurnOnInfo info = GetDefensePhaseByBeHitBox(hitBox);
                        int thisPriority = hitBox.Priority + info.priority;
                        if (best == null || thisPriority > bestPriority)
                        {
                            best = hitBox;
                            bestPriority = thisPriority;
                            defensePhase = info;
                        }
                    }

                    if (best == null) continue;

                    attackBox = touch.Key;
                    targetBox = best;
                    if (action.CurrentAction.attacks != null && boxInfo.attackPhase >= 0 && boxInfo.attackPhase < action.CurrentAction.attacks.Length)
                    {
                        attackPhase = action.CurrentAction.attacks[boxInfo.attackPhase];
                    }
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 添加或更新命中记录 (使用定点数冷却)
        /// </summary>
        public void AddHitRecord(CharacterObj target, int attackPhase)
        {
            if (target == null || action == null) return;

            int idx = action.IndexOfAttack(attackPhase);
            if (idx < 0 || action.CurrentAction.attacks == null || idx >= action.CurrentAction.attacks.Length) return;

            var atk = action.CurrentAction.attacks[idx];
            HitRecord rec = GetHitRecord(target, attackPhase);
            if (rec == null)
            {
                HitRecords.Add(new HitRecord(target, attackPhase, atk.canHitSameTarget - 1, atk.hitSameTargetDelay));
            }
            else
            {
                rec.Cooldown = atk.hitSameTargetDelay;
                rec.CanHitTimes -= 1;
            }
        }

        public HitRecord GetHitRecord(CharacterObj target, int phase)
        {
            if (target == null) return null;
            int targetId = target.gameObject.GetInstanceID();
            for (int i = 0; i < HitRecords.Count; i++)
            {
                if (HitRecords[i].UniqueId == targetId && HitRecords[i].Phase == phase)
                    return HitRecords[i];
            }
            return null;
        }
    }
}
