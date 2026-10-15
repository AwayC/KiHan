using System;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作逻辑控制器 (ActionController)
    /// 核心职责：
    /// 1. 基于纯整数帧驱动当前动作推进 (由 FrameAnimator 驱动)
    /// 2. 匹配 CancelTag 与 BeCancelledTag 整数帧打断窗口，结合 InputToCommand 决策出招
    /// 3. 管理自动流向 (autoNextActionId) 与循环动作维持 (keepPlayingAnim)
    /// 4. 激活当前帧攻击盒与受击盒，计算移动速度倍率
    /// </summary>
    public class ActionController : MonoBehaviour
    {
        [Tooltip("角色的动画播放器 (纯整数帧驱动)")]
        public FrameAnimator anim;

        /// <summary>
        /// 角色的指令输入解析器 (由 CharacterObj.input 统一设置，不单独配置)
        /// </summary>
        public InputToCommand command { get; internal set; }

        /// <summary>
        /// 所属角色 (由 CharacterObj.action 设置)：接收动作切换与动作事件，并提供技能准入裁决
        /// </summary>
        public CharacterObj Owner { get; internal set; }

        /// <summary>
        /// 动作序号：每次切换动作加 1 (同动作重入同样加 1，循环重播不变)，表现层据此判断事件所属的动作是否已结束
        /// </summary>
        public int ActionSerial { get; private set; }

        /// <summary>
        /// 受击中强制进入 (breakHurt) 动作的预约优先级基数：高于命中切换受击动作 (HurtActionIds.Priority)
        /// </summary>
        public const int BreakHurtPriority = 200;

        /// <summary>
        /// 当前正在执行的动作数据
        /// </summary>
        public ActionInfo CurrentAction { get; private set; }

        private bool _facingLeft;

        /// <summary>
        /// 角色朝向 (权威逻辑状态)：是否面朝左
        /// 判定盒、RootMotion、平推、吸附与抓取锚点均按此水平镜像；进入 inverse 动作时翻转 (随动作切换在 FlushPreorders 生效)
        /// 表现层直接读取此朝向翻转精灵；输入只记录绝对左右，指令中的前/后在匹配时按此朝向换算
        /// </summary>
        public bool FacingLeft
        {
            get => _facingLeft;
            set => _facingLeft = value;
        }

        /// <summary>
        /// 当前激活的被取消标签窗口列表 (含静态配置与运行时动态临时标签)
        /// </summary>
        public List<BeCancelledTag> CurrentBeCancelledTag { get; private set; } = new List<BeCancelledTag>();

        /// <summary>
        /// 角色当前已学会的全部动作库
        /// </summary>
        public List<ActionInfo> AllActions { get; private set; } = new List<ActionInfo>();

        /// <summary>
        /// 当前帧的动作切换预约候选列表
        /// </summary>
        private List<PreorderActionInfo> _preorderActions = new List<PreorderActionInfo>(16);

        /// <summary>
        /// 按 Catalog 挑选受击动作时复用的候选列表 (避免每次命中分配)
        /// </summary>
        private readonly List<ActionInfo> _catalogCandidates = new List<ActionInfo>();

        /// <summary>
        /// 当前动作的 RootMotion 分段
        /// </summary>
        private RootMotionSegment[] _rootMotion = Array.Empty<RootMotionSegment>();

        /// <summary>
        /// 当前帧激活的攻击盒 Tag 与详情列表
        /// </summary>
        public List<string> ActiveAttackBoxTag { get; private set; } = new List<string>();
        public List<AttackBoxTurnOnInfo> ActiveAttackBoxInfo { get; private set; } = new List<AttackBoxTurnOnInfo>();

        /// <summary>
        /// 本帧横向 (x) 移动速度接受率 (inputAcceptance 中生效区间的 rateX，多段重叠取最小)
        /// </summary>
        public FP MoveRateX { get; private set; } = FP.Zero;

        /// <summary>
        /// 本帧纵深 (y) 移动速度接受率 (inputAcceptance 中生效区间的 rateY，多段重叠取最小)
        /// </summary>
        public FP MoveRateY { get; private set; } = FP.Zero;

        /// <summary>
        /// 本帧是否允许随摇杆转向 (inputAcceptance 中 turn 为 true 的区间)
        /// </summary>
        public bool TurnAllowed { get; private set; }

        /// <summary>
        /// 当前帧激活的受击盒 Tag 与详情列表
        /// </summary>
        public List<string> ActiveBeHitBoxTag { get; private set; } = new List<string>();
        public List<BeHitBoxTurnOnInfo> ActiveBeHitBoxInfo { get; private set; } = new List<BeHitBoxTurnOnInfo>();

        /// <summary>
        /// 当前帧的 RootMotion 水平位移增量 (x 横向, y 纵深；z 恒为 0，竖直方向只走速度模型)
        /// </summary>
        public FPVector3 RootMotionMove { get; private set; } = FPVector3.zero;

        /// <summary>
        /// 本帧是否触发竖直速度设置 (起跳 / 二段跳 / 下砸)，为 true 时物理层把竖直速度设为 VelocityZ
        /// </summary>
        public bool VelocityZTriggered { get; private set; }

        /// <summary>
        /// 本帧设置的竖直速度 (同一逻辑帧触发多段时取列表中靠后的一段)
        /// </summary>
        public FP VelocityZ { get; private set; } = FP.Zero;

        /// <summary>
        /// 本帧动作附加的竖直加速度 (单位/秒²，多个区间重叠时累加)
        /// </summary>
        public FP ZAcceleration { get; private set; } = FP.Zero;

        /// <summary>
        /// 本帧是否触发水平惯性速度设置，为 true 时物理层把水平惯性设为 PlanarVelocity (按朝向镜像)
        /// </summary>
        public bool PlanarVelocityTriggered { get; private set; }

        /// <summary>
        /// 本帧设置的水平惯性速度 (x 面朝方向前方, y 纵深；同一逻辑帧触发多段时取列表中靠后的一段)
        /// </summary>
        public FPVector3 PlanarVelocity { get; private set; } = FPVector3.zero;

        /// <summary>
        /// 受控锁定 (受击状态)：为 true 时屏蔽一切主动打断、摇杆移动与 autoNext，动作流转只接受命中/落地/受击流程的预约
        /// </summary>
        public bool ControlLocked { get; private set; }

        /// <summary>
        /// 默认动作 (SetAllActions 指定，通常为 idle)：受击流程结束后回到此动作
        /// </summary>
        public string DefaultActionId { get; private set; } = string.Empty;

        /// <summary>
        /// 当前霸体等级 (按本逻辑帧最新处理的动画帧计算)
        /// </summary>
        public ArmorLevel CurrentArmor { get; private set; } = ArmorLevel.None;

        /// <summary>
        /// 锁定期间当前动作播完后循环播放 (僵直 / 被抓)；为 false 时停在最后一帧，由受击流程预约下一个动作
        /// </summary>
        private bool _loopWhileLocked;

        /// <summary>
        /// 当前动作执行到的整数逻辑帧数
        /// </summary>
        private int _curFrame = 0;
        private int _wasFrame = 0;

        /// <summary>
        /// 核心驱动：由房间逻辑帧主循环显式调用 (纯确定性推进，严禁使用 Time.deltaTime)
        /// </summary>
        public void Tick()
        {
            if (AllActions.Count <= 0 || anim == null) return;

            // 1. 推进动画
            anim.Tick();

            _curFrame = anim.ElapsedFrames;

            // 3. 计算攻击盒与受击盒开启状态、霸体等级
            CalculateBoxInfo(_wasFrame, _curFrame);
            CalculateArmor(_wasFrame, _curFrame);

            // 4. 计算移动输入接受倍率与转向许可，并随摇杆转向 (先于物理与命中检测，本帧即按新朝向镜像)
            CalculateInputAcceptance(_wasFrame, _curFrame);
            ApplyInputTurn();

            // 5. RootMotion 分段：水平缓动位移、竖直速度设置、竖直附加加速度
            CalculateRootMotion(_wasFrame, _curFrame);

            // 6. 动作时间轴事件
            FireEvents(_wasFrame, _curFrame);

            // 7. 遍历动作库检测主动打断 (受控锁定时不能主动行动，只检测受击中强制进入的动作)
            if (!ControlLocked)
            {
                foreach (ActionInfo action in AllActions)
                {
                    if (CanActionCancelCurrent(action, _curFrame, true, out BeCancelledTag bcTag, out CancelTag cancelTag))
                    {
                        _preorderActions.Add(new PreorderActionInfo(
                            action.id,
                            bcTag.priority + cancelTag.priority + action.priority,
                            cancelTag.startFormFrame
                        ));
                    }
                }
            }
            else
            {
                PreorderBreakHurtActions();
            }

            // 8. 自然播放结束或输入中断 (autoTerminate) 时预约 autoNext；受控锁定时不走 autoNext (循环或由受击流程驱动)
            if (!ControlLocked && _preorderActions.Count <= 0 && (anim.IsFinished || CurrentAction.autoTerminate))
            {
                if (!string.IsNullOrEmpty(CurrentAction.autoNextActionId))
                {
                    _preorderActions.Add(new PreorderActionInfo(CurrentAction.autoNextActionId));
                }
            }

            // 9. 动作切换不在此处执行：统一延迟到 FlushPreorders 裁决
            //    保证本逻辑帧的物理与命中检测使用的仍是实际推进了 [TickFromFrame, TickToFrame) 这些动画帧的动作
            _wasFrame = _curFrame;
        }

        /// <summary>
        /// 裁决本逻辑帧的全部动作预约 (主动打断 / autoNext / 落地 / 命中)，按优先级当帧生效
        /// 由 BattleWorld 在本帧物理与打击结算全部完成后调用
        /// </summary>
        public void FlushPreorders()
        {
            ResolvePreorders();
        }

        /// <summary>
        /// 物理事件回调：滞空落地时按当前动作的 onLand 预约动作变化
        /// </summary>
        public void OnPhysicsEvent(PhysicsEvent evt)
        {
            if ((evt & PhysicsEvent.Landed) != 0 && CurrentAction.onLand.changeType != ActionChangeType.Keep)
            {
                PreorderActionByActionChangeInfo(CurrentAction.onLand, ForceDirection.Forward);
            }
        }

        /// <summary>
        /// 取优先级最高的预约 (同优先级先到先得，保证确定性)，执行后清空预约列表
        /// 没有任何预约且处于僵直循环时，受击动作播完则从头循环
        /// </summary>
        private void ResolvePreorders()
        {
            if (_preorderActions.Count <= 0 && ControlLocked && _loopWhileLocked && anim != null && anim.IsFinished)
            {
                RestartCurrentAnim();
            }
            else if (_preorderActions.Count > 0)
            {
                PreorderActionInfo winner = _preorderActions[0];
                for (int i = 1; i < _preorderActions.Count; i++)
                {
                    if (_preorderActions[i].Priority > winner.Priority) winner = _preorderActions[i];
                }

                if (winner.ActionId == CurrentAction.id && CurrentAction.keepPlayingAnim)
                {
                    KeepAction(_curFrame);
                }
                else
                {
                    ChangeAction(winner.ActionId, winner.FromFrame, winner.KeepFrame);
                }
            }

            _preorderActions.Clear();
        }

        /// <summary>
        /// 进入受控锁定
        /// </summary>
        /// <param name="loopCurrentAction">锁定期间受击动作播完是否循环播放 (僵直/被抓为 true；击飞/倒地/起身为 false，由受击流程驱动)</param>
        public void Lock(bool loopCurrentAction)
        {
            ControlLocked = true;
            _loopWhileLocked = loopCurrentAction;
        }

        /// <summary>
        /// 解除受控锁定 (之后的动作由调用方预约)
        /// </summary>
        public void Unlock()
        {
            ControlLocked = false;
            _loopWhileLocked = false;
        }

        /// <summary>
        /// 触发本逻辑帧处理过的动画帧 [wasFrame, curFrame) 上的动作事件 (本帧未推进动画帧时不触发)
        /// </summary>
        private void FireEvents(int wasFrame, int curFrame)
        {
            ActionEventInfo[] events = CurrentAction.events;
            if (events == null || Owner == null || curFrame <= wasFrame) return;

            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].frame >= wasFrame && events[i].frame < curFrame)
                {
                    Owner.OnActionEvent(in events[i]);
                }
            }
        }

        /// <summary>
        /// 受控锁定 (受击) 期间：只检测可强制进入的 breakHurt 动作 (替身术)，忽略被取消窗口，需指令匹配且通过技能准入
        /// </summary>
        private void PreorderBreakHurtActions()
        {
            if (command == null) return;

            foreach (ActionInfo action in AllActions)
            {
                if (!action.breakHurt || !MatchCommand(action)) continue;
                if (Owner != null && !Owner.CanEnterAction(action)) continue;

                _preorderActions.Add(new PreorderActionInfo(action.id, BreakHurtPriority + action.priority, 0));
            }
        }

        private bool MatchCommand(ActionInfo action)
        {
            if (command == null || action.commands == null) return false;
            foreach (ActionCommand ac in action.commands)
            {
                if (command.ActionOccur(ac, _facingLeft)) return true;
            }
            return false;
        }

        /// <summary>
        /// 霸体等级取本逻辑帧最新处理的动画帧 (未推进时取当前帧) 上开启的最高等级
        /// </summary>
        private void CalculateArmor(int wasFrame, int curFrame)
        {
            CurrentArmor = ArmorLevel.None;
            if (CurrentAction.armorPhase == null) return;

            int frame = curFrame > wasFrame ? curFrame - 1 : curFrame;
            foreach (ArmorPhaseInfo phase in CurrentAction.armorPhase)
            {
                if (frame >= phase.range.x && frame <= phase.range.y && phase.level > CurrentArmor)
                {
                    CurrentArmor = phase.level;
                }
            }
        }

        /// <summary>
        /// 按本逻辑帧处理过的动画帧 [wasFrame, curFrame) 计算全部 RootMotion 分段 (本帧未推进动画帧时区间为空，全部为 0)
        /// - 水平：每段位移 = move × (进度(curFrame) - 进度(wasFrame))，多段相加
        /// - 竖直速度：区间覆盖分段起始帧时触发
        /// - 竖直加速度：按区间内实际覆盖的帧数折算，保证一个逻辑帧包含多个动画帧时与逐动画帧积分一致
        /// </summary>
        private void CalculateRootMotion(int wasFrame, int curFrame)
        {
            FP moveX = FP.Zero;
            FP moveY = FP.Zero;
            VelocityZTriggered = false;
            VelocityZ = FP.Zero;
            ZAcceleration = FP.Zero;
            PlanarVelocityTriggered = false;
            PlanarVelocity = FPVector3.zero;

            int stride = curFrame - wasFrame;
            if (stride > 0 && _rootMotion != null)
            {
                for (int i = 0; i < _rootMotion.Length; i++)
                {
                    RootMotionSegment seg = _rootMotion[i];

                    if (seg.HasMove)
                    {
                        FP progress = seg.MoveProgress(curFrame) - seg.MoveProgress(wasFrame);
                        moveX += seg.moveX * progress;
                        moveY += seg.moveY * progress;
                    }

                    if (seg.hasVelocityZ && wasFrame <= seg.frameRange.x && seg.frameRange.x < curFrame)
                    {
                        VelocityZTriggered = true;
                        VelocityZ = seg.velocityZ;
                    }

                    if (seg.hasPlanarVelocity && wasFrame <= seg.frameRange.x && seg.frameRange.x < curFrame)
                    {
                        PlanarVelocityTriggered = true;
                        PlanarVelocity = new FPVector3(seg.velocityX, seg.velocityY, FP.Zero);
                    }

                    if (seg.accelerationZ != FP.Zero)
                    {
                        int overlap = System.Math.Min(curFrame, seg.frameRange.y + 1) - System.Math.Max(wasFrame, seg.frameRange.x);
                        if (overlap > 0) ZAcceleration += seg.accelerationZ * overlap / stride;
                    }
                }
            }

            RootMotionMove = new FPVector3(moveX, moveY, FP.Zero);
        }

        public void CalculateInputAcceptance(int wasFrame, int curFrame)
        {
            MoveRateX = FP.Zero;
            MoveRateY = FP.Zero;
            TurnAllowed = false;
            if (CurrentAction.inputAcceptance == null) return;
            bool any = false;
            foreach (MoveInputAcceptance acceptance in CurrentAction.inputAcceptance)
            {
                if (wasFrame <= acceptance.range.y && curFrame > acceptance.range.x)
                {
                    // 多段重叠时各轴分别取最小 (0 也参与比较)
                    if (!any || acceptance.rateX < MoveRateX) MoveRateX = acceptance.rateX;
                    if (!any || acceptance.rateY < MoveRateY) MoveRateY = acceptance.rateY;
                    any = true;
                    if (acceptance.turn) TurnAllowed = true;
                }
            }
        }

        /// <summary>
        /// 随摇杆转向：本帧处于允许转向的区间且未被受控锁定时，朝向跟随摇杆水平方向
        /// </summary>
        private void ApplyInputTurn()
        {
            if (!TurnAllowed || ControlLocked || command == null) return;
            int sign = command.HorizontalSign;
            if (sign < 0 && !_facingLeft) FacingLeft = true;
            else if (sign > 0 && _facingLeft) FacingLeft = false;
        }

        private void CalculateBoxInfo(int wasFrame, int curFrame)
        {
            ActiveAttackBoxInfo.Clear();
            ActiveAttackBoxTag.Clear();
            if (CurrentAction.attackPhase != null)
            {
                foreach (AttackBoxTurnOnInfo aBox in CurrentAction.attackPhase)
                {
                    bool open = false;
                    if (aBox.frameRanges != null)
                    {
                        foreach (FrameRange range in aBox.frameRanges)
                        {
                            // 本逻辑帧处理过的动画帧 [wasFrame, curFrame) 与 [x, y] 有交集即开启
                            if (wasFrame <= range.y && curFrame > range.x)
                            {
                                open = true;
                                break;
                            }
                        }
                    }

                    if (open)
                    {
                        if (aBox.tag != null)
                        {
                            foreach (string aTag in aBox.tag)
                            {
                                if (!ActiveAttackBoxTag.Contains(aTag)) ActiveAttackBoxTag.Add(aTag);
                            }
                        }
                        ActiveAttackBoxInfo.Add(aBox);
                    }
                }
            }

            ActiveBeHitBoxInfo.Clear();
            ActiveBeHitBoxTag.Clear();
            if (CurrentAction.defensePhase != null)
            {
                foreach (BeHitBoxTurnOnInfo bHitBox in CurrentAction.defensePhase)
                {
                    bool open = false;
                    if (bHitBox.frameRanges != null)
                    {
                        foreach (FrameRange range in bHitBox.frameRanges)
                        {
                            if (curFrame >= range.x && curFrame <= range.y)
                            {
                                open = true;
                                break;
                            }
                        }
                    }

                    if (open)
                    {
                        if (bHitBox.tag != null)
                        {
                            foreach (string bTag in bHitBox.tag)
                            {
                                if (!ActiveBeHitBoxTag.Contains(bTag)) ActiveBeHitBoxTag.Add(bTag);
                            }
                        }
                        ActiveBeHitBoxInfo.Add(bHitBox);
                    }
                }
            }
        }

        private bool CanActionCancelCurrent(ActionInfo action, int curFrame, bool checkCommand, out BeCancelledTag beCancelledTag, out CancelTag foundTag)
        {
            foundTag = new CancelTag();
            beCancelledTag = new BeCancelledTag();

            if (action.cancelTag == null || action.cancelTag.Length == 0) return false;

            foreach (BeCancelledTag bcTag in CurrentBeCancelledTag)
            {
                // 整数帧区间重叠检验 [range.x, range.y]
                if (!(bcTag.range.y >= curFrame && bcTag.range.x <= curFrame)) continue;

                // 判断 CancelTag 是否有交集
                bool tagFit = false;
                foreach (string cTag in bcTag.cancelTag)
                {
                    foreach (CancelTag cancelTag in action.cancelTag)
                    {
                        if (cancelTag.tag == cTag)
                        {
                            tagFit = true;
                            beCancelledTag = bcTag;
                            foundTag = cancelTag;
                            break;
                        }
                    }
                    if (tagFit) break;
                }

                if (!tagFit) continue;

                // 技能准入 (CD / 多段 / 查克拉 / 受击条件)：与取消窗口无关，不通过时直接放弃
                if (Owner != null && !Owner.CanEnterAction(action)) return false;

                // 检查按键指令匹配：配置了指令的动作必须由输入触发 (无输入组件的角色不会误触发)
                if (checkCommand && action.commands != null && action.commands.Length > 0)
                {
                    if (command == null) continue;
                    foreach (ActionCommand ac in action.commands)
                    {
                        if (command.ActionOccur(ac, _facingLeft)) return true;
                    }
                }
                else return true;
            }

            return false;
        }

        private void ChangeAction(string actionId, int fromFrame, bool keepFrame = false)
        {
            ActionInfo aInfo = GetActionById(actionId, out bool foundAction);
            if (foundAction)
            {
                command?.ConsumePresses();

                ActionInfo previous = CurrentAction;
                anim?.CrossFade(aInfo.animKey, 0, fromFrame);
                CurrentAction = aInfo;
                ActionSerial++;

                CurrentBeCancelledTag.Clear();
                if (aInfo.beCancelledTag != null)
                {
                    CurrentBeCancelledTag.AddRange(aInfo.beCancelledTag);
                }

                ActiveBeHitBoxInfo.Clear();
                ActiveBeHitBoxTag.Clear();
                ActiveAttackBoxTag.Clear();
                ActiveAttackBoxInfo.Clear();

                _rootMotion = aInfo.rootMotion ?? Array.Empty<RootMotionSegment>();
                VelocityZTriggered = false;
                VelocityZ = FP.Zero;
                ZAcceleration = FP.Zero;
                PlanarVelocityTriggered = false;
                PlanarVelocity = FPVector3.zero;
                RootMotionMove = FPVector3.zero;
                _curFrame = fromFrame;
                _wasFrame = fromFrame;

                // 面向翻转处理 (与是否挂有输入组件无关)
                FacingLeft = aInfo.inverse ? !_facingLeft : _facingLeft;

                Owner?.OnActionChanged(previous, aInfo, keepFrame);
            }
        }

        /// <summary>
        /// 复用实例时重置到指定动作 (清除预约与受控锁定，不重新校验动作库)
        /// </summary>
        public void ResetTo(string actionId)
        {
            _preorderActions.Clear();
            Unlock();
            ChangeAction(actionId, 0);
        }

        private void KeepAction(int currentFrame)
        {
            if (anim != null && anim.IsFinished)
            {
                RestartCurrentAnim();
            }
        }

        /// <summary>
        /// 当前动作从第 0 帧重新播放 (同步重置帧游标，避免 RootMotion/区间判定以旧帧号计算出负增量)
        /// </summary>
        private void RestartCurrentAnim()
        {
            anim?.Play(CurrentAction.animKey, 0);
            _curFrame = 0;
            _wasFrame = 0;
        }

        private ActionInfo GetActionById(string actionId, out bool found)
        {
            found = false;
            foreach (ActionInfo action in AllActions)
            {
                if (action.id == actionId)
                {
                    found = true;
                    return action;
                }
            }

            return CurrentAction;
        }

        public int IndexOfAttack(int attackPhase)
        {
            if (CurrentAction.attacks == null) return -1;
            for (int i = 0; i < CurrentAction.attacks.Length; i++)
            {
                // 如果 AttackInfo 包含 phase 字段
                if (i == attackPhase) return i;
            }
            return -1;
        }

        public void SetAllActions(List<ActionInfo> actions, string defaultActionId)
        {
            // 直接替换引用：不能先 Clear 旧列表，否则传入的正是当前列表时会把动作库清空
            AllActions = actions ?? new List<ActionInfo>();
            DefaultActionId = defaultActionId ?? string.Empty;

            // 通用受击动作必须全部配置 (受击流程按固定 id 切换动作)
            List<string> missing = HurtActionIds.FindMissing(AllActions);
            if (missing.Count > 0)
            {
                Debug.LogError($"[ActionController] 缺少通用受击动作: {string.Join(", ", missing)}");
            }

            ValidateGrabAnchors();
            ChangeAction(defaultActionId, 0);
        }

        /// <summary>
        /// 校验抓取锚点 (需先注册动画)：抓取攻击段开启区间内的每一帧及命中后的下一帧都必须画有抓取锚点，
        /// 否则命中后会因锚点消失立即释放
        /// </summary>
        private void ValidateGrabAnchors()
        {
            if (anim == null) return;

            foreach (ActionInfo info in AllActions)
            {
                if (info.attacks == null || info.attackPhase == null) continue;
                int total = anim.GetTotalFrames(info.animKey);
                if (total <= 0) continue;

                foreach (AttackBoxTurnOnInfo phase in info.attackPhase)
                {
                    int idx = phase.attackPhase;
                    if (idx < 0 || idx >= info.attacks.Length || !info.attacks[idx].grab.enabled || phase.frameRanges == null) continue;

                    string anchorName = string.IsNullOrEmpty(info.attacks[idx].grab.anchor) ? GrabInfo.DefaultAnchor : info.attacks[idx].grab.anchor;
                    foreach (FrameRange range in phase.frameRanges)
                    {
                        int to = Mathf.Min(range.y + 1, total - 1);
                        for (int f = Mathf.Max(0, range.x); f <= to; f++)
                        {
                            if (!anim.TryGetAnchorAtFrame(info.animKey, f, anchorName, out _))
                            {
                                Debug.LogError($"[ActionController] 动作 {info.id} 的抓取段 {idx} 在第 {f} 帧缺少锚点 \"{anchorName}\" (动画 {info.animKey})");
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void PreorderActionByActionChangeInfo(ActionChangeInfo acInfo, ForceDirection forceDir)
        {
            switch (acInfo.changeType)
            {
                case ActionChangeType.Keep:
                    break;

                case ActionChangeType.ChangeByCatalog:
                    List<ActionInfo> actions = _catalogCandidates;
                    actions.Clear();
                    foreach (ActionInfo info in AllActions)
                    {
                        if (info.catalog == acInfo.param) actions.Add(info);
                    }

                    if (actions.Count > 0)
                    {
                        ActionInfo picked = actions[0];
                        if (PickActionMethod.Methods.ContainsKey(acInfo.param))
                        {
                            picked = PickActionMethod.Methods[acInfo.param](actions, forceDir);
                        }

                        _preorderActions.Add(new PreorderActionInfo(
                            picked.id,
                            acInfo.priority + picked.priority,
                            acInfo.keepFrame ? _curFrame : acInfo.fromFrame,
                            acInfo.keepFrame
                        ));
                    }
                    break;

                case ActionChangeType.ChangeToActionId:
                    ActionInfo aInfo = GetActionById(acInfo.param, out bool found);
                    if (found)
                    {
                        _preorderActions.Add(new PreorderActionInfo(
                            aInfo.id,
                            acInfo.priority + aInfo.priority,
                            acInfo.keepFrame ? _curFrame : acInfo.fromFrame,
                            acInfo.keepFrame
                        ));
                    }
                    break;
            }
        }

        public void AddTempBeCancelledTag(TempBeCancelledTag beCancelledTag)
        {
            CurrentBeCancelledTag.Add(BeCancelledTag.FromTemp(beCancelledTag, _curFrame));
        }

        public void AddTempBeCancelledTag(string tempTagId)
        {
            if (CurrentAction.tempBeCancelledTag == null) return;
            foreach (TempBeCancelledTag beCancelledTag in CurrentAction.tempBeCancelledTag)
            {
                if (beCancelledTag.id == tempTagId)
                {
                    AddTempBeCancelledTag(beCancelledTag);
                    return;
                }
            }
        }
    }
}
