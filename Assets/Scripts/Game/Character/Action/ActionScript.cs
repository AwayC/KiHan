using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作指令的触发时机
    /// </summary>
    public enum ScriptTrigger
    {
        /// <summary>
        /// 动画推进经过 frame 时执行一次；frame 等于进入帧 (通常为 0) 的指令在切入动作的同一逻辑帧立即执行
        /// </summary>
        Frame,

        /// <summary>
        /// 本动作的攻击在 range 内的动画帧命中时执行 (每个动作实例只执行一次，被霸体抵挡、目标被抓中同样计入)
        /// </summary>
        Hit,

        /// <summary>
        /// 动画处于 range 内时按下 keys 执行 (每个动作实例只执行一次，受击锁定期间不检测)
        /// </summary>
        Input,

        /// <summary>
        /// 离开本动作时执行 (任何原因：播完、取消、受击)；不能使用 goto
        /// </summary>
        Exit
    }

    /// <summary>
    /// 动作指令
    /// </summary>
    public enum ScriptOp
    {
        /// <summary>
        /// 无法识别的指令 (加载时报错，运行时忽略)
        /// </summary>
        None,

        /// <summary>
        /// 预约切换到 action，优先级 = priority + 当前动作 priority (不经过技能准入，受击锁定期间忽略)
        /// </summary>
        Goto,

        /// <summary>
        /// 变量 name = value (角色级整型变量，跨动作保留，初始为 0)
        /// </summary>
        SetVar,

        /// <summary>
        /// 变量 name += value
        /// </summary>
        AddVar,

        /// <summary>
        /// 技能 skill 的按键换成 action (下一段)：window 秒内可按键释放，期间 CD 清零、入口动作被拦截；
        /// 超时恢复按键并开始 CD (返还 cdReturn)；释放 action 时恢复按键并开始完整 CD
        /// </summary>
        SkillStage,

        /// <summary>
        /// 立即恢复技能 skill 的按键并开始 CD (返还 cdReturn)
        /// </summary>
        SkillEnd,

        /// <summary>
        /// 清空本动作对各目标的命中记录 (已命中的目标可再次被命中)
        /// </summary>
        ResetHits,

        /// <summary>
        /// 给自己 (主人) 加一层 Buff buff，持续 window 秒 (留空按 buffs 配置的时长)；hit 指令配 toTarget 时加给被打中的目标
        /// </summary>
        AddBuff,

        /// <summary>
        /// 移除自己 (主人) 身上 Buff buff 的全部层；hit 指令配 toTarget 时移除被打中的目标身上的
        /// </summary>
        RemoveBuff
    }

    /// <summary>
    /// 条件比较符
    /// </summary>
    public enum CompareOp
    {
        Equal,
        NotEqual,
        Less,
        LessEqual,
        Greater,
        GreaterEqual
    }

    /// <summary>
    /// 指令条件：变量 name (或自己身上 Buff buff 的剩余帧数) 与 value 比较
    /// </summary>
    public struct ScriptCondition
    {
        public string name;

        /// <summary>
        /// 不为空时比较自己 (主人) 身上该 Buff 剩余最多一层的剩余帧数 (没有为 0)，而不是变量
        /// </summary>
        public string buff;

        public CompareOp cmp;
        public int value;

        public bool Test(int variable)
        {
            switch (cmp)
            {
                case CompareOp.NotEqual: return variable != value;
                case CompareOp.Less: return variable < value;
                case CompareOp.LessEqual: return variable <= value;
                case CompareOp.Greater: return variable > value;
                case CompareOp.GreaterEqual: return variable >= value;
                default: return variable == value;
            }
        }
    }

    /// <summary>
    /// 动作指令 (运行时，时间已换算为逻辑帧)：触发时机满足、条件全部成立时执行 op
    /// </summary>
    public struct ActionScript
    {
        public ScriptTrigger on;

        /// <summary>
        /// Frame 触发的动画帧
        /// </summary>
        public int frame;

        /// <summary>
        /// Hit / Input 触发的动画帧闭区间
        /// </summary>
        public Vector2Int range;

        /// <summary>
        /// Input 触发的按键 (全部按下)
        /// </summary>
        public ActionCommand input;

        public ScriptCondition[] cond;
        public ScriptOp op;

        /// <summary>
        /// goto 的目标动作 / skillStage 换上的下一段动作
        /// </summary>
        public string action;

        public int priority;

        /// <summary>
        /// setVar / addVar 的变量名
        /// </summary>
        public string name;

        public int value;

        /// <summary>
        /// skillStage / skillEnd 的技能 id
        /// </summary>
        public string skill;

        /// <summary>
        /// addBuff / removeBuff 的 Buff id
        /// </summary>
        public string buff;

        /// <summary>
        /// addBuff / removeBuff 作用于被打中的目标 (只用于 hit 指令)
        /// </summary>
        public bool toTarget;

        public int windowTicks;
        public int cdReturnTicks;

        /// <summary>
        /// skillStage 期间的按键图标 (仅表现层使用)，为空沿用技能图标
        /// </summary>
        public string icon;
    }

    /// <summary>
    /// 指令条件 DTO：{ "name": "hit", "cmp": "==", "value": 1 }，或 Buff { "buff": "attackEx", "cmp": ">", "value": 0 }
    /// </summary>
    [Serializable]
    public struct ScriptConditionData
    {
        [Tooltip("变量名 (与 buff 二选一)")]
        public string name;

        [Tooltip("Buff id：比较自己身上该 Buff 的剩余帧数 (没有为 0)，与 name 二选一")]
        public string buff;

        [Tooltip("比较符：== != < <= > >= (留空为 ==)")]
        public string cmp;

        public int value;

        public static readonly string[] CompareSymbols = { "==", "!=", "<", "<=", ">", ">=" };

        public static bool TryParseCompare(string symbol, out CompareOp op)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                op = CompareOp.Equal;
                return true;
            }
            int index = Array.IndexOf(CompareSymbols, symbol.Trim());
            op = index >= 0 ? (CompareOp)index : CompareOp.Equal;
            return index >= 0;
        }

        public ScriptCondition ToRuntime()
        {
            if (!TryParseCompare(cmp, out CompareOp op))
            {
                Debug.LogError($"[ActionData] 无法识别的比较符: \"{cmp}\"，可选值: {string.Join(" ", CompareSymbols)}");
            }
            return new ScriptCondition { name = name ?? string.Empty, buff = buff ?? string.Empty, cmp = op, value = value };
        }
    }

    /// <summary>
    /// 动作指令 DTO (String-First：时间为字符串秒数)
    /// 写法示例：
    /// { "frame": 0, "op": "setVar", "name": "hit", "value": 0 }
    /// { "on": "hit", "range": { "x": 19, "y": 26 }, "op": "setVar", "name": "hit", "value": 1 }
    /// { "frame": 9, "cond": [{ "name": "hit", "cmp": "==", "value": 1 }], "op": "goto", "action": "skill2_2", "priority": 50 }
    /// { "frame": 0, "op": "skillStage", "skill": "skill1", "action": "skill1_2", "window": "3", "cdReturn": "4" }
    /// { "frame": 20, "op": "addBuff", "buff": "attackEx" }
    /// { "on": "hit", "range": { "x": 5, "y": 8 }, "op": "addBuff", "buff": "mark", "toTarget": true }
    /// </summary>
    [Serializable]
    public struct ActionScriptData
    {
        [Tooltip("触发时机：frame (默认) / hit / input / exit")]
        public string on;

        [Tooltip("frame 触发的动画帧")]
        public int frame;

        [Tooltip("hit / input 触发的动画帧区间 { x: 起始帧, y: 结束帧 }")]
        public FrameRange range;

        [Tooltip("input 触发的按键 (全部按下)，如 [\"Skill1\"]")]
        public string[] keys;

        [Tooltip("执行条件 (全部成立才执行)")]
        public ScriptConditionData[] cond;

        [Tooltip("指令：goto / setVar / addVar / skillStage / skillEnd / resetHits / addBuff / removeBuff")]
        public string op;

        [Tooltip("goto 的目标动作 / skillStage 换上的下一段动作")]
        public string action;

        [Tooltip("goto 的预约优先级 (与当前动作 priority 相加)")]
        public int priority;

        [Tooltip("setVar / addVar 的变量名")]
        public string name;

        [Tooltip("setVar / addVar 的值")]
        public int value;

        [Tooltip("skillStage / skillEnd 的技能 id (skills.json)")]
        public string skill;

        [Tooltip("addBuff / removeBuff 的 Buff id (buffs.json)")]
        public string buff;

        [Tooltip("addBuff / removeBuff 作用于被打中的目标 (只用于 hit 指令)")]
        public bool toTarget;

        [Tooltip("skillStage 的接段时限 (字符串秒数，必须大于 0)；addBuff 的持续时间 (留空按 Buff 配置)")]
        public string window;

        [Tooltip("skillStage 超时 / skillEnd 时返还的 CD (字符串秒数)")]
        public string cdReturn;

        [Tooltip("skillStage 期间的按键图标，留空沿用技能图标")]
        public string icon;

        public static bool TryParseTrigger(string name, out ScriptTrigger trigger)
        {
            trigger = ScriptTrigger.Frame;
            return string.IsNullOrEmpty(name) || (Enum.TryParse(name.Trim(), true, out trigger) && Enum.IsDefined(typeof(ScriptTrigger), trigger));
        }

        public static bool TryParseOp(string name, out ScriptOp op)
        {
            op = ScriptOp.None;
            return !string.IsNullOrEmpty(name) && Enum.TryParse(name.Trim(), true, out op) && Enum.IsDefined(typeof(ScriptOp), op) && op != ScriptOp.None;
        }

        public ActionScript ToRuntime()
        {
            if (!TryParseTrigger(on, out ScriptTrigger trigger))
            {
                Debug.LogError($"[ActionData] 无法识别的指令触发时机: \"{on}\"，可选值: frame / hit / input / exit");
            }
            if (!TryParseOp(op, out ScriptOp parsedOp))
            {
                Debug.LogError($"[ActionData] 无法识别的指令: \"{op}\"，可选值: goto / setVar / addVar / skillStage / skillEnd / resetHits / addBuff / removeBuff");
            }

            var script = new ActionScript
            {
                on = trigger,
                frame = frame,
                range = new Vector2Int(range.x, range.y),
                op = parsedOp,
                action = action ?? string.Empty,
                priority = priority,
                name = name ?? string.Empty,
                value = value,
                skill = skill ?? string.Empty,
                buff = buff ?? string.Empty,
                toTarget = toTarget,
                windowTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(window)),
                cdReturnTicks = GameConfig.SecondsToTicks(ActionData.ParseFP(cdReturn)),
                icon = icon ?? string.Empty,
                cond = new ScriptCondition[cond != null ? cond.Length : 0]
            };
            for (int i = 0; i < script.cond.Length; i++) script.cond[i] = cond[i].ToRuntime();
            if (trigger == ScriptTrigger.Input) script.input = new ActionCommandData { press = keys }.ToRuntime();
            return script;
        }
    }
}
