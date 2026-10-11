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
        public RootMotionSegmentData[] rootMotion;
        public int priority;
        public ActionChangeData onLand;

        public ArmorPhaseData[] armorPhase;

        /// <summary>
        /// 全局通用确定性字符串 -> FP 解析器 (纯整型位移，0 浮点乘除法)
        /// </summary>
        public static FP ParseFP(string strVal)
        {
            if (string.IsNullOrEmpty(strVal)) return FP.Zero;
            return FP.Parse(strVal);
        }

        /// <summary>
        /// 字符串 -> 枚举 (按名称解析，忽略大小写)，为空或无法识别时返回默认值
        /// </summary>
        public static T ParseEnum<T>(string strVal, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(strVal)) return defaultValue;
            return Enum.TryParse(strVal, true, out T result) ? result : defaultValue;
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
            if (rootMotion != null)
            {
                info.rootMotion = new RootMotionSegment[rootMotion.Length];
                for (int i = 0; i < rootMotion.Length; i++)
                {
                    info.rootMotion[i] = rootMotion[i].ToRuntime();
                }
            }
            else
            {
                info.rootMotion = Array.Empty<RootMotionSegment>();
            }
            info.priority = priority;
            info.onLand = onLand != null ? onLand.ToRuntime() : default;

            if (armorPhase != null)
            {
                info.armorPhase = new ArmorPhaseInfo[armorPhase.Length];
                for (int i = 0; i < armorPhase.Length; i++)
                {
                    info.armorPhase[i] = armorPhase[i].ToRuntime();
                }
            }
            else
            {
                info.armorPhase = Array.Empty<ArmorPhaseInfo>();
            }

            // 1. 转换 BeCancelledTag (支持 frameRange 或 range 字段)
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
        public FrameRange frameRange;
        public Vector2Int range;
        public string[] cancelTag;
        public int priority;

        public BeCancelledTag ToRuntime()
        {
            Vector2Int finalRange = (frameRange.max > 0 || frameRange.min > 0)
                ? new Vector2Int(frameRange.min, frameRange.max)
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
    /// 输入招式命令配置 DTO (按键采用 CommandKey 名称，如 ["Down", "Skill1"]；时间采用字符串秒数，如 "0.15")
    /// </summary>
    [Serializable]
    public struct ActionCommandData
    {
        [Tooltip("按键顺序序列 (CommandKey 名称，忽略大小写，如 [\"Down\", \"Skill1\"])")]
        public string[] keySequence;

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
                    keys[i] = ParseKey(keySequence[i]);
                }
            }

            return new ActionCommand(keys, ActionData.ParseFP(validInSec));
        }

        /// <summary>
        /// 按键名称 -> CommandKey；无法识别时报错并返回 None (None 不会被录入输入记录，该招式不会误触发)
        /// </summary>
        private static CommandKey ParseKey(string name)
        {
            if (!string.IsNullOrEmpty(name) && Enum.TryParse(name.Trim(), true, out CommandKey key) && Enum.IsDefined(typeof(CommandKey), key))
            {
                return key;
            }

            Debug.LogError($"[ActionData] 无法识别的按键名称: \"{name}\"，可选值: {string.Join(", ", Enum.GetNames(typeof(CommandKey)))}");
            return CommandKey.None;
        }
    }

    /// <summary>
    /// 移动速度接受配置 DTO (采用字符串比率，如 "1.0", "0.5")
    /// </summary>
    [Serializable]
    public struct MoveInputAcceptanceData
    {
        public FrameRange range;

        [Tooltip("速度接受倍率 (字符串，如 \"1.0\", \"0.5\")")]
        public string rate;

        public MoveInputAcceptance ToRuntime()
        {
            return new MoveInputAcceptance
            {
                range = new Vector2Int(range.min, range.max),
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

        [Tooltip("目标僵直时长 (字符串秒数，如 \"0.3\")，留空表示不施加僵直")]
        public string hitStun;

        [Tooltip("击飞竖直初速度 (字符串，单位/秒，如 \"10\")，留空表示不击飞")]
        public string launchVelocity;

        [Tooltip("命中空中目标时的竖直速度 (字符串，单位/秒)，留空沿用 launchVelocity")]
        public string airLaunchVelocity;

        [Tooltip("破霸等级：None / Skill / Super / KingKong")]
        public string armorBreak;

        [Tooltip("能否命中倒地目标 (扫地)")]
        public bool canHitLying;

        public GrabData grab;
        public SuctionData suction;

        [Tooltip("停帧时长 (字符串秒数)，预留给全局停帧，当前未生效")]
        public string freeze;

        public int canHitSameTarget;

        [Tooltip("连击同一目标的冷却间隔 (字符串秒数，如 \"1.0\")")]
        public string hitSameTargetDelay;

        public ActionChangeData selfActionChange;
        public ActionChangeData targetActionChange;
        public ActionChangeData launchActionChange;
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
                launchVelocity = ActionData.ParseFP(launchVelocity),
                airLaunchVelocity = ActionData.ParseFP(airLaunchVelocity),
                armorBreak = ActionData.ParseEnum(armorBreak, ArmorLevel.None),
                canHitLying = canHitLying,
                grab = grab.ToRuntime(),
                suction = suction.ToRuntime(),
                freeze = ActionData.ParseFP(freeze),
                canHitSameTarget = canHitSameTarget > 0 ? canHitSameTarget : 1,
                hitSameTargetDelay = ActionData.ParseFP(hitSameTargetDelay),
                selfActionChange = selfActionChange != null ? selfActionChange.ToRuntime() : default,
                targetActionChange = targetActionChange != null ? targetActionChange.ToRuntime() : default,
                launchActionChange = launchActionChange != null ? launchActionChange.ToRuntime() : default,
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
    /// 霸体区间配置 DTO (等级采用名称字符串，如 "Super")
    /// </summary>
    [Serializable]
    public struct ArmorPhaseData
    {
        [Tooltip("生效的动画帧区间 [min, max] (30fps 素材帧)")]
        public FrameRange range;

        [Tooltip("霸体等级：None / Skill / Super / KingKong / Invincible")]
        public string level;

        public ArmorPhaseInfo ToRuntime()
        {
            return new ArmorPhaseInfo
            {
                range = range,
                level = ActionData.ParseEnum(level, ArmorLevel.None)
            };
        }
    }

    /// <summary>
    /// 抓取配置 DTO
    /// </summary>
    [Serializable]
    public struct GrabData
    {
        public bool enabled;

        [Tooltip("抓取锚点名称 (帧库 EffectAnchors)，留空使用 \"grab\"")]
        public string anchor;

        public ActionChangeData releaseActionChange;

        public GrabInfo ToRuntime()
        {
            return new GrabInfo
            {
                enabled = enabled,
                anchor = string.IsNullOrEmpty(anchor) ? GrabInfo.DefaultAnchor : anchor,
                releaseActionChange = releaseActionChange != null ? releaseActionChange.ToRuntime() : default
            };
        }
    }

    /// <summary>
    /// 吸附配置 DTO
    /// </summary>
    [Serializable]
    public struct SuctionData
    {
        [Tooltip("吸附核心点本地偏移 (字符串，x 前方, y 纵深)")]
        public string centerX;
        public string centerY;

        [Tooltip("拉扯速度 (字符串，单位/秒)")]
        public string speed;

        [Tooltip("持续时间 (字符串秒数)")]
        public string inSec;

        public SuctionInfo ToRuntime()
        {
            return new SuctionInfo
            {
                center = new FPVector3(ActionData.ParseFP(centerX), ActionData.ParseFP(centerY), FP.Zero),
                speed = ActionData.ParseFP(speed),
                inSec = ActionData.ParseFP(inSec)
            };
        }
    }

    /// <summary>
    /// RootMotion 分段配置 DTO (数值采用字符串，如 "0.6")
    /// </summary>
    [Serializable]
    public struct RootMotionSegmentData
    {
        [Tooltip("生效的动画帧区间 [min, max] (30fps 素材帧)")]
        public FrameRange frameRange;

        [Tooltip("本段水平总位移 x (字符串，面朝方向前方为正)")]
        public string moveX;

        [Tooltip("本段水平总位移 y (字符串，纵深)")]
        public string moveY;

        [Tooltip("水平位移缓动：Linear / Slowly，留空为 Linear")]
        public string tweenMethod;

        [Tooltip("在 frameRange.min 帧设置的竖直速度 (字符串，单位/秒)，留空表示不设置")]
        public string velocityZ;

        [Tooltip("区间内附加的竖直加速度 (字符串，单位/秒²，向上为正)")]
        public string accelerationZ;

        public RootMotionSegment ToRuntime()
        {
            return new RootMotionSegment
            {
                frameRange = frameRange,
                moveX = ActionData.ParseFP(moveX),
                moveY = ActionData.ParseFP(moveY),
                tweenMethod = tweenMethod ?? string.Empty,
                hasVelocityZ = !string.IsNullOrEmpty(velocityZ),
                velocityZ = ActionData.ParseFP(velocityZ),
                accelerationZ = ActionData.ParseFP(accelerationZ)
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
