using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// Action.json 反序列化专用顶层容器 DTO
    /// </summary>
    [Serializable]
    public class ActionDataContainer
    {
        public ActionData[] data;
    }

    /// <summary>
    /// 动作原始配置 DTO
    /// 关键定点数字段全量支持字符串声明 (如 "0.15", "0.08")，保证 100% 纯整型解析确定性
    /// </summary>
    [Serializable]
    public class ActionData
    {
        public string id;
        public string animKey;
        public string catalog;
        public CancelTag[] cancelTag;
        public BeCancelledTagData[] beCancelledTag;
        public TempBeCancelledTag[] tempBeCancelledTag;
        public ActionCommandData[] commands;
        public MoveInputAcceptanceData[] inputAcceptance;
        public string autoNextActionId;
        public bool keepPlayingAnim;
        public bool autoTerminate;
        public bool inverse;
        public string facing;
        public AttackData[] attacks;
        public AttackBoxTurnOnInfo[] attackPhase;
        public BeHitBoxTurnOnInfo[] defensePhase;
        public ScriptMethodInfo rootMotionTween;
        public int priority;

        /// <summary>
        /// 全局通用确定性字符串 -> FP 解析器 (纯整型位移，0 浮点乘除法)
        /// </summary>
        public static FP ParseFP(string strVal)
        {
            if (string.IsNullOrEmpty(strVal)) return FP.Zero;
            return FP.Parse(strVal);
        }

        /// <summary>
        /// 转换为逻辑内核运行时纯定点数结构体 (100% 确定性转换，0 跨平台浮点误差)
        /// </summary>
        public ActionInfo ToRuntime()
        {
            ActionInfo info = new ActionInfo();
            info.id = id ?? string.Empty;
            info.animKey = animKey ?? string.Empty;
            info.catalog = catalog ?? string.Empty;
            info.cancelTag = cancelTag ?? Array.Empty<CancelTag>();
            info.tempBeCancelledTag = tempBeCancelledTag ?? Array.Empty<TempBeCancelledTag>();
            info.autoNextActionId = autoNextActionId ?? string.Empty;
            info.keepPlayingAnim = keepPlayingAnim;
            info.autoTerminate = autoTerminate;
            info.inverse = inverse;
            info.facing = facing ?? string.Empty;
            info.attackPhase = attackPhase ?? Array.Empty<AttackBoxTurnOnInfo>();
            info.defensePhase = defensePhase ?? Array.Empty<BeHitBoxTurnOnInfo>();
            info.rootMotionTween = rootMotionTween;
            info.priority = priority;

            // 1. 转换 BeCancelledTag (支持 percentageRange 或 range 字段)
            if (beCancelledTag != null)
            {
                info.beCancelledTag = new BeCancelledTag[beCancelledTag.Length];
                for (int i = 0; i < beCancelledTag.Length; i++)
                {
                    info.beCancelledTag[i] = beCancelledTag[i].ToRuntime();
                }
            }
            else
            {
                info.beCancelledTag = Array.Empty<BeCancelledTag>();
            }

            // 2. 转换指令序列与字符串定点数有效时间窗口 (validInSec -> FP)
            if (commands != null)
            {
                info.commands = new ActionCommand[commands.Length];
                for (int i = 0; i < commands.Length; i++)
                {
                    info.commands[i] = commands[i].ToRuntime();
                }
            }
            else
            {
                info.commands = Array.Empty<ActionCommand>();
            }

            // 3. 转换移动速度接受比率 (rate -> FP)
            if (inputAcceptance != null)
            {
                info.inputAcceptance = new MoveInputAcceptance[inputAcceptance.Length];
                for (int i = 0; i < inputAcceptance.Length; i++)
                {
                    info.inputAcceptance[i] = inputAcceptance[i].ToRuntime();
                }
            }
            else
            {
                info.inputAcceptance = Array.Empty<MoveInputAcceptance>();
            }

            // 4. 转换攻击数据 (全量时间字段通过字符串解析为 FP)
            if (attacks != null)
            {
                info.attacks = new AttackInfo[attacks.Length];
                for (int i = 0; i < attacks.Length; i++)
                {
                    info.attacks[i] = attacks[i].ToRuntime();
                }
            }
            else
            {
                info.attacks = Array.Empty<AttackInfo>();
            }

            return info;
        }
    }

    /// <summary>
    /// 被打断窗口配置 DTO
    /// </summary>
    [Serializable]
    public struct BeCancelledTagData
    {
        public PercentageRange percentageRange;
        public Vector2 range;
        public string[] cancelTag;
        public int priority;

        public BeCancelledTag ToRuntime()
        {
            Vector2 finalRange = (percentageRange.max > 0 || percentageRange.min > 0)
                ? new Vector2(percentageRange.min, percentageRange.max)
                : range;

            return new BeCancelledTag
            {
                range = finalRange,
                cancelTag = cancelTag ?? Array.Empty<string>(),
                priority = priority
            };
        }
    }

    /// <summary>
    /// 输入招式命令配置 DTO (采用字符串秒数，如 "0.15")
    /// </summary>
    [Serializable]
    public struct ActionCommandData
    {
        public int[] keySequence;

        [Tooltip("招式有效时间窗口 (字符串秒数，如 \"0.15\")")]
        public string validInSec;

        public ActionCommand ToRuntime()
        {
            CommandKey[] keys = null;
            if (keySequence != null)
            {
                keys = new CommandKey[keySequence.Length];
                for (int i = 0; i < keySequence.Length; i++)
                {
                    keys[i] = (CommandKey)keySequence[i];
                }
            }

            return new ActionCommand(keys, ActionData.ParseFP(validInSec));
        }
    }

    /// <summary>
    /// 移动速度接受配置 DTO (采用字符串比率，如 "1.0", "0.5")
    /// </summary>
    [Serializable]
    public struct MoveInputAcceptanceData
    {
        public PercentageRange range;

        [Tooltip("速度接受倍率 (字符串，如 \"1.0\", \"0.5\")")]
        public string rate;

        public MoveInputAcceptance ToRuntime()
        {
            return new MoveInputAcceptance
            {
                range = new Vector2(range.min, range.max),
                rate = ActionData.ParseFP(rate)
            };
        }
    }

    /// <summary>
    /// 攻击数据配置 DTO (时间与倍率全量采用纯字符串配置)
    /// </summary>
    [Serializable]
    public struct AttackData
    {
        public int phase;

        [Tooltip("伤害或伤害倍率 (字符串，如 \"10.0\")")]
        public string attack;

        public int forceDir;
        public MoveInfoData pushPower;

        [Tooltip("目标受击硬直时间 (字符串秒数，如 \"0.12\")")]
        public string hitStun;

        [Tooltip("自身卡帧停顿时间 (字符串秒数，如 \"0.05\")")]
        public string freeze;

        public int canHitSameTarget;

        [Tooltip("连击同一目标的冷却间隔 (字符串秒数，如 \"1.0\")")]
        public string hitSameTargetDelay;

        public ActionChangeData selfActionChange;
        public ActionChangeData targetActionChange;
        public string[] tempBeCancelledTagTurnOn;

        public AttackInfo ToRuntime()
        {
            return new AttackInfo
            {
                phase = phase,
                attack = ActionData.ParseFP(attack),
                forceDir = (ForceDirection)forceDir,
                pushPower = pushPower.ToRuntime(),
                hitStun = ActionData.ParseFP(hitStun),
                freeze = ActionData.ParseFP(freeze),
                canHitSameTarget = canHitSameTarget > 0 ? canHitSameTarget : 1,
                hitSameTargetDelay = ActionData.ParseFP(hitSameTargetDelay),
                selfActionChange = selfActionChange != null ? selfActionChange.ToRuntime() : default,
                targetActionChange = targetActionChange != null ? targetActionChange.ToRuntime() : default,
                tempBeCancelledTagTurnOn = tempBeCancelledTagTurnOn ?? Array.Empty<string>()
            };
        }
    }

    /// <summary>
    /// 受击/技能推力位移配置 DTO (支持纯字符串三维位移与持续时间)
    /// </summary>
    [Serializable]
    public struct MoveInfoData
    {
        [Tooltip("X轴位移 (字符串，如 \"0.1\")")] public string moveX;
        [Tooltip("Y轴位移 (字符串，如 \"0.0\")")] public string moveY;
        [Tooltip("Z轴位移 (字符串，如 \"0.0\")")] public string moveZ;

        [Tooltip("持续位移秒数 (字符串秒数，如 \"0.08\")")]
        public string inSec;

        public string tweenMethod;

        // 兼容原版浮点 Vector3
        public Vector3 moveDistance;

        public MoveInfo ToRuntime()
        {
            FPVector3 dist = (!string.IsNullOrEmpty(moveX) || !string.IsNullOrEmpty(moveY) || !string.IsNullOrEmpty(moveZ))
                ? new FPVector3(ActionData.ParseFP(moveX), ActionData.ParseFP(moveY), ActionData.ParseFP(moveZ))
                : FPVector3.FromVector3Deterministic(moveDistance);

            return new MoveInfo
            {
                moveDistance = dist,
                inSec = ActionData.ParseFP(inSec),
                tweenMethod = tweenMethod ?? string.Empty
            };
        }
    }

    /// <summary>
    /// 动作变化预约配置 DTO (采用字符串硬直停顿秒数)
    /// </summary>
    [Serializable]
    public class ActionChangeData
    {
        public int changeType;
        public string param;
        public int priority;
        public int fromFrame;

        [Tooltip("切换动作后产生的硬直停顿时间 (字符串秒数，如 \"0.2\")")]
        public string freezingAfterChange;

        public ActionChangeInfo ToRuntime()
        {
            return new ActionChangeInfo
            {
                changeType = (ActionChangeType)changeType,
                param = param ?? string.Empty,
                priority = priority,
                fromFrame = fromFrame,
                freezingAfterChange = ActionData.ParseFP(freezingAfterChange)
            };
        }
    }
}
