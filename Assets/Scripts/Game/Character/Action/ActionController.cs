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
    /// 4. 激活当前帧攻击盒与受击盒，计算移动速度倍率与受击硬直停顿
    /// </summary>
    public class ActionController : MonoBehaviour
    {
        [Tooltip("角色的动画播放器 (纯整数帧驱动)")]
        public FrameAnimator anim;

        [Tooltip("角色的指令输入解析器")]
        public InputToCommand command;

        /// <summary>
        /// 当前正在执行的动作数据
        /// </summary>
        public ActionInfo CurrentAction { get; private set; }

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
        private List<PreorderActionInfo> _preorderActions = new List<PreorderActionInfo>();

        /// <summary>
        /// 当前动作的 RootMotion 算法配置
        /// </summary>
        private ScriptMethodInfo _rootMotion = new ScriptMethodInfo();

        /// <summary>
        /// 当前帧激活的攻击盒 Tag 与详情列表
        /// </summary>
        public List<string> ActiveAttackBoxTag { get; private set; } = new List<string>();
        public List<AttackBoxTurnOnInfo> ActiveAttackBoxInfo { get; private set; } = new List<AttackBoxTurnOnInfo>();

        /// <summary>
        /// 当前帧的移动速度接受比率 [0.0 ~ 1.0] (定点数)
        /// </summary>
        public FP MoveInputAcceptance { get; private set; } = FP.Zero;

        /// <summary>
        /// 当前帧激活的受击盒 Tag 与详情列表
        /// </summary>
        public List<string> ActiveBeHitBoxTag { get; private set; } = new List<string>();
        public List<BeHitBoxTurnOnInfo> ActiveBeHitBoxInfo { get; private set; } = new List<BeHitBoxTurnOnInfo>();

        /// <summary>
        /// 当前帧的 RootMotion 位移增量 (定点数向量)
        /// </summary>
        public FPVector3 RootMotionMove { get; private set; } = FPVector3.zero;

        /// <summary>
        /// 动作切换回调 (上一个动作, 切换后的动作)
        /// </summary>
        private Action<ActionInfo, ActionInfo> _onChangeAction = null;

        /// <summary>
        /// 硬直/卡帧逻辑帧数 (HitStop Ticks)
        /// </summary>
        private int _freezeTicks = 0;
        public bool Freezing => _freezeTicks > 0;

        /// <summary>
        /// 当前动作执行到的整数逻辑帧数
        /// </summary>
        private int _curFrame = 0;
        private int _wasFrame = 0;

        public void Update() => Tick();

        /// <summary>
        /// 核心驱动：由房间逻辑帧主循环显式调用 (纯确定性推进，严禁使用 Time.deltaTime)
        /// </summary>
        public void Tick()
        {
            if (AllActions.Count <= 0 || anim == null) return;

            // 1. 卡帧停顿 (HitStop / Freeze)
            if (_freezeTicks > 0)
            {
                _freezeTicks--;
            }
            else anim.Tick();

            _curFrame = anim.ElapsedFrames;

            // 3. 计算攻击盒与受击盒开启状态
            CalculateBoxInfo(_wasFrame, _curFrame);

            // 4. 计算移动输入接受倍率
            CalculateInputAcceptance(_wasFrame, _curFrame);

            // 5. 计算当前帧的 RootMotion 位移变化 (基于定点数)
            if (!string.IsNullOrEmpty(_rootMotion.method) && RootMotionMethod.Methods.ContainsKey(_rootMotion.method))
            {
                FP curPec = anim.TotalFrames > 0 ? (FP)_curFrame / (FP)anim.TotalFrames : FP.Zero;
                FP wasPec = anim.TotalFrames > 0 ? (FP)_wasFrame / (FP)anim.TotalFrames : FP.Zero;
                FPVector3 rmThisTick = RootMotionMethod.Methods[_rootMotion.method](curPec, _rootMotion.param);
                FPVector3 rmLastTick = RootMotionMethod.Methods[_rootMotion.method](wasPec, _rootMotion.param);
                RootMotionMove = rmThisTick - rmLastTick;
            }
            else
            {
                RootMotionMove = FPVector3.zero;
            }

            // 6. 遍历动作库检测主动打断
            foreach (ActionInfo action in AllActions)
            {
                if (CanActionCancelCurrent(action, _curFrame, true, out BeCancelledTag bcTag, out CancelTag cancelTag))
                {
                    _preorderActions.Add(new PreorderActionInfo(
                        action.id,
                        bcTag.priority + cancelTag.priority + action.priority,
                        cancelTag.startFormFrame,
                        FP.Zero
                    ));
                }
            }

            // 7. 自然播放结束或输入中断 (autoTerminate) 时预约下一个动作
            if (_preorderActions.Count <= 0 && (anim.IsFinished || CurrentAction.autoTerminate))
            {
                if (!string.IsNullOrEmpty(CurrentAction.autoNextActionId))
                {
                    _preorderActions.Add(new PreorderActionInfo(CurrentAction.autoNextActionId));
                }
            }

            // 8. 冒泡裁决最高优先级动作
            _wasFrame = _curFrame;
            if (_preorderActions.Count > 0)
            {
                _preorderActions.Sort((c1, c2) => c2.Priority.CompareTo(c1.Priority));
                var winner = _preorderActions[0];

                if (winner.ActionId == CurrentAction.id && CurrentAction.keepPlayingAnim)
                {
                    KeepAction(_curFrame);
                }
                else
                {
                    ChangeAction(winner.ActionId, winner.FromFrame, winner.FreezingAfterChangeAction);
                }
            }

            _preorderActions.Clear();
        }

        public void Set(Action<ActionInfo, ActionInfo> onActionChanged)
        {
            _onChangeAction = onActionChanged;
        }

        public void CalculateInputAcceptance(int wasFrame, int curFrame)
        {
            MoveInputAcceptance = FP.Zero;
            if (CurrentAction.inputAcceptance == null) return;
            foreach (MoveInputAcceptance acceptance in CurrentAction.inputAcceptance)
            {
                if (acceptance.range.x <= curFrame && acceptance.range.y >= wasFrame &&
                    (MoveInputAcceptance <= FP.Zero || acceptance.rate < MoveInputAcceptance))
                {
                    MoveInputAcceptance = acceptance.rate;
                }
            }
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
                    if (aBox.inPercentage != null)
                    {
                        foreach (PercentageRange range in aBox.inPercentage)
                        {
                            if (curFrame >= range.min && wasFrame <= range.max)
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
                    if (bHitBox.inPercentage != null)
                    {
                        foreach (PercentageRange range in bHitBox.inPercentage)
                        {
                            if (curFrame >= range.min && curFrame <= range.max)
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

                // 检查按键指令匹配
                if (checkCommand && command != null)
                {
                    if (action.commands != null && action.commands.Length > 0)
                    {
                        foreach (ActionCommand ac in action.commands)
                        {
                            if (command.ActionOccur(ac)) return true;
                        }
                    }
                    else return true;
                }
                else return true;
            }

            return false;
        }

        private void ChangeAction(string actionId, int fromFrame, FP freezingAfterChange)
        {
            ActionInfo aInfo = GetActionById(actionId, out bool foundAction);
            if (foundAction)
            {
                command?.CleanNonDirectionInputs();

                _onChangeAction?.Invoke(CurrentAction, aInfo);
                anim?.CrossFade(aInfo.animKey, 0, fromFrame);
                CurrentAction = aInfo;

                CurrentBeCancelledTag.Clear();
                if (aInfo.beCancelledTag != null)
                {
                    CurrentBeCancelledTag.AddRange(aInfo.beCancelledTag);
                }

                _freezeTicks = (int)FP.Ceiling(freezingAfterChange * GameConfig.LOGIC_FPS);
                if (anim != null && _freezeTicks > 0) anim.SetFreezing(_freezeTicks);

                ActiveBeHitBoxInfo.Clear();
                ActiveBeHitBoxTag.Clear();
                ActiveAttackBoxTag.Clear();
                ActiveAttackBoxInfo.Clear();

                _rootMotion = aInfo.rootMotionTween;
                _curFrame = fromFrame;
                _wasFrame = fromFrame;

                // 面向翻转处理
                if (command != null)
                {
                    if (aInfo.inverse) command.inversed = !command.inversed;
                    if (anim != null) anim.IsFacingLeft = command.inversed;
                }
            }
        }

        private void KeepAction(int currentFrame)
        {
            if (anim != null && anim.IsFinished)
            {
                anim.Play(CurrentAction.animKey, 0);
            }
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
            AllActions.Clear();
            if (actions != null) AllActions = actions;
            ChangeAction(defaultActionId, 0, FP.Zero);
        }

        public void PreorderActionByActionChangeInfo(ActionChangeInfo acInfo, ForceDirection forceDir, FP freezing = default)
        {
            FP finalFreeze = freezing > FP.Zero ? freezing : acInfo.freezingAfterChange;
            switch (acInfo.changeType)
            {
                case ActionChangeType.Keep:
                    break;

                case ActionChangeType.ChangeByCatalog:
                    List<ActionInfo> actions = new List<ActionInfo>();
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
                            acInfo.fromFrame,
                            finalFreeze
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
                            acInfo.fromFrame,
                            finalFreeze
                        ));
                    }
                    break;
            }
        }

        public void PreorderActionByActionChangeInfo(ActionChangeInfo acInfo, ForceDirection forceDir, float freezingSec)
        {
            PreorderActionByActionChangeInfo(acInfo, forceDir, FP.FromFloatDeterministic(freezingSec));
        }

        /// <summary>
        /// 设置卡帧/打击停顿 (以房间确定性逻辑帧为单位)
        /// </summary>
        public void SetFreezing(int freezeTicks)
        {
            _freezeTicks = System.Math.Max(_freezeTicks, freezeTicks);
            if (anim != null) anim.SetFreezing(_freezeTicks);
        }

        public void SetFreezing(float freezingSec)
        {
            int ticks = Mathf.CeilToInt(freezingSec / GameConfig.LOGIC_TICK_TIME);
            SetFreezing(ticks);
        }
        public void SetFreezing(FP freezingSec)
        {
            int ticks = (int)FP.Ceiling(freezingSec * GameConfig.LOGIC_FPS);
            SetFreezing(ticks);
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
