using System;
using System.Collections.Generic;
using KiHan.Logic;
using UnityEditor;
using UnityEngine;

namespace KiHan.Editor
{
    public partial class ActionEditorWindow
    {
        private struct Issue
        {
            public int Action;
            public bool Error;
            public string Text;
        }

        private List<Issue> _validation;
        private Vector2 _validationScroll;
        private bool _validationFold = true;

        /// <summary>
        /// 配置校验：与运行时加载规则一致 (按键名、枚举名、动作引用、伤害段下标、帧区间)
        /// </summary>
        private List<Issue> Validate()
        {
            var issues = new List<Issue>();
            if (_state == null) return issues;

            var ids = new Dictionary<string, int>();
            foreach (ActionEditorState.Entry entry in _state.entries)
            {
                string id = entry.action.id ?? string.Empty;
                ids[id] = ids.TryGetValue(id, out int n) ? n + 1 : 1;
            }

            for (int i = 0; i < _state.entries.Count; i++)
            {
                ActionData a = _state.entries[i].action;
                void Err(string text) => issues.Add(new Issue { Action = i, Error = true, Text = $"{a.id}: {text}" });
                void Warn(string text) => issues.Add(new Issue { Action = i, Error = false, Text = $"{a.id}: {text}" });

                if (string.IsNullOrEmpty(a.id)) Err("id 为空");
                else if (ids[a.id] > 1) Err("id 重复");

                if (string.IsNullOrEmpty(a.animKey)) Err("animKey 为空");
                else if (!_anims.ContainsKey(a.animKey)) Err($"动画 {a.animKey} 不在角色目录中");

                var refs = new List<KeyValuePair<string, string>>();
                CollectOutgoing(a, refs);
                foreach (KeyValuePair<string, string> r in refs)
                {
                    if (FindAction(r.Value) >= 0) continue;
                    // 对手受击动作属于被打的一方，可能是其他角色的动作
                    if (r.Key.StartsWith("对手", StringComparison.Ordinal)) Warn($"{r.Key} 指向本角色不存在的动作 {r.Value}");
                    else Err($"{r.Key} 指向不存在的动作 {r.Value}");
                }

                int total = TotalFrames(a);
                var items = new List<TimelineItem>();
                foreach (TrackInfo track in Tracks)
                {
                    items.Clear();
                    CollectItems(a, track.Kind, items);
                    foreach (TimelineItem item in items)
                    {
                        if (item.X < 0 || item.Y < item.X) Err($"{track.Field}[{item.Index}] 区间 [{item.X}, {item.Y}] 无效");
                        else if (item.X >= total) Warn($"{track.Field}[{item.Index}] 起点 {item.X} 超出动画 ({total} 帧)，不会生效");
                    }
                }

                int attackCount = a.attacks?.Length ?? 0;
                if (a.attackPhase != null)
                {
                    for (int p = 0; p < a.attackPhase.Length; p++)
                    {
                        int phase = a.attackPhase[p].attackPhase;
                        if (phase < 0 || phase >= attackCount) Err($"attackPhase[{p}] 指向 attacks[{phase}]，但只有 {attackCount} 段伤害");
                        if (a.attackPhase[p].frameRanges == null || a.attackPhase[p].frameRanges.Length == 0) Warn($"attackPhase[{p}] 没有 frameRanges，不会开启");
                    }
                }

                if (a.attacks != null)
                {
                    for (int k = 0; k < a.attacks.Length; k++)
                    {
                        AttackData atk = a.attacks[k];
                        CheckEnum<ArmorLevel>(atk.armorBreak, $"attacks[{k}].armorBreak", Err);
                        CheckTempTags(a, atk.tempBeCancelledTagTurnOn, $"attacks[{k}].tempBeCancelledTagTurnOn", Err);
                    }
                }

                if (a.defensePhase != null)
                {
                    for (int d = 0; d < a.defensePhase.Length; d++)
                    {
                        CheckTempTags(a, a.defensePhase[d].tempBeCancelledTagTurnOn, $"defensePhase[{d}].tempBeCancelledTagTurnOn", Err);
                    }
                }

                if (a.armorPhase != null)
                {
                    for (int p = 0; p < a.armorPhase.Length; p++) CheckEnum<ArmorLevel>(a.armorPhase[p].level, $"armorPhase[{p}].level", Err);
                }

                if (a.events != null)
                {
                    for (int ev = 0; ev < a.events.Length; ev++)
                    {
                        ActionEventData e = a.events[ev];
                        CheckEnum<ActionEventType>(e.type, $"events[{ev}].type", Err);
                        ActionEventType type = ActionData.ParseEnum(e.type, ActionEventType.Effect);
                        if (type == ActionEventType.Buff || type == ActionEventType.BuffRemoved) Err($"events[{ev}] 动作事件不能使用 {type} 类型，给角色加 Buff 改用 scripts 的 addBuff");
                        if (type == ActionEventType.Spawn && string.IsNullOrEmpty(e.spawnId)) Err($"events[{ev}] Spawn 事件缺少 spawnId");
                        if (type != ActionEventType.Spawn && string.IsNullOrEmpty(e.asset)) Err($"events[{ev}] {type} 事件缺少 asset");
                        if (e.loop && !e.bind) Warn($"events[{ev}] 循环特效未绑定角色 (bind)");
                        CheckEnum<CinematicAudience>(e.audience, $"events[{ev}].audience", Err);
                        if (type != ActionEventType.Cinematic && type != ActionEventType.Camera && !string.IsNullOrEmpty(e.audience)) Warn($"events[{ev}] audience 只对 Cinematic / Camera 生效");
                        CheckNumber(e.worldPos.x, $"events[{ev}].worldPos.x", Err);
                        CheckNumber(e.worldPos.y, $"events[{ev}].worldPos.y", Err);
                        CheckNumber(e.worldPos.z, $"events[{ev}].worldPos.z", Err);
                        if (e.worldPos.IsSet && type != ActionEventType.Effect && type != ActionEventType.Spawn && type != ActionEventType.Cinematic && !(type == ActionEventType.Camera && e.asset == CameraEffects.Shot))
                            Warn($"events[{ev}] worldPos 只对 Effect / Spawn / Cinematic / Camera shot 生效");
                        if (e.worldPos.IsSet && e.bind) Warn($"events[{ev}] 指定了 worldPos，bind 不生效 (世界坐标位置不跟随角色)");
                        if (type == ActionEventType.Camera) CheckCamera(e, ev, Err, Warn);
                    }
                }

                if (a.commands != null)
                {
                    for (int c = 0; c < a.commands.Length; c++)
                    {
                        ActionCommandData cmd = a.commands[c];
                        int count = CheckKeys(cmd.press, false, $"commands[{c}].press", Err) + CheckKeys(cmd.hold, true, $"commands[{c}].hold", Err) + CheckKeys(cmd.release, false, $"commands[{c}].release", Err);
                        if (count == 0) Err($"commands[{c}] 没有配置任何按键");
                    }
                }

                if (a.scripts != null)
                {
                    if (a.scripts.Length > ActionController.MaxScriptsPerAction) Err($"scripts 超过 {ActionController.MaxScriptsPerAction} 条 ({a.scripts.Length})，超出部分的 hit / input 不执行");
                    for (int s = 0; s < a.scripts.Length; s++) CheckScript(a.scripts[s], s, total, Err, Warn);
                }

                if (a.rootMotion != null)
                {
                    for (int r = 0; r < a.rootMotion.Length; r++)
                    {
                        RootMotionSegmentData seg = a.rootMotion[r];
                        CheckNumber(seg.moveX, $"rootMotion[{r}].moveX", Err);
                        CheckNumber(seg.moveY, $"rootMotion[{r}].moveY", Err);
                        CheckNumber(seg.velocityX, $"rootMotion[{r}].velocityX", Err);
                        CheckNumber(seg.velocityY, $"rootMotion[{r}].velocityY", Err);
                        CheckNumber(seg.velocityZ, $"rootMotion[{r}].velocityZ", Err);
                        CheckNumber(seg.accelerationZ, $"rootMotion[{r}].accelerationZ", Err);
                    }
                }
            }
            return issues;
        }

        /// <summary>
        /// 动作指令：触发时机、op、条件、帧 / 区间、按键、技能换段参数 (动作引用由 CollectOutgoing 统一检查)
        /// </summary>
        private static void CheckScript(ActionScriptData s, int index, int total, Action<string> err, Action<string> warn)
        {
            string field = $"scripts[{index}]";
            if (!ActionScriptData.TryParseTrigger(s.on, out ScriptTrigger trigger)) err($"{field}.on = \"{s.on}\" 无法识别，可选值: frame / hit / input / exit");
            bool opOk = ActionScriptData.TryParseOp(s.op, out ScriptOp op);
            if (!opOk) err($"{field}.op = \"{s.op}\" 无法识别，可选值: goto / setVar / addVar / skillStage / skillEnd / resetHits / addBuff / removeBuff");

            if (s.cond != null)
            {
                for (int c = 0; c < s.cond.Length; c++)
                {
                    bool hasName = !string.IsNullOrEmpty(s.cond[c].name);
                    bool hasBuff = !string.IsNullOrEmpty(s.cond[c].buff);
                    if (hasName == hasBuff) err($"{field}.cond[{c}] 需要且只能配置 name (变量) 或 buff (Buff 剩余帧数) 其中之一");
                    if (!ScriptConditionData.TryParseCompare(s.cond[c].cmp, out _)) err($"{field}.cond[{c}].cmp = \"{s.cond[c].cmp}\" 无法识别，可选值: {string.Join(" ", ScriptConditionData.CompareSymbols)}");
                }
            }

            if (trigger == ScriptTrigger.Frame)
            {
                if (s.frame < 0) err($"{field}.frame = {s.frame} 无效");
                else if (s.frame >= total) warn($"{field}.frame = {s.frame} 超出动画 ({total} 帧)，不会生效");
            }
            else if (trigger == ScriptTrigger.Hit || trigger == ScriptTrigger.Input)
            {
                if (s.range.x < 0 || s.range.y < s.range.x) err($"{field}.range [{s.range.x}, {s.range.y}] 无效");
                else if (s.range.x >= total) warn($"{field}.range 起点 {s.range.x} 超出动画 ({total} 帧)，不会生效");
            }
            if (trigger == ScriptTrigger.Input)
            {
                if (CheckKeys(s.keys, false, $"{field}.keys", err) == 0) err($"{field} input 触发没有配置任何按键");
            }
            else if (s.keys != null && s.keys.Length > 0) warn($"{field}.keys 只对 input 触发生效");

            if (!opOk) return;
            switch (op)
            {
                case ScriptOp.Goto:
                    if (trigger == ScriptTrigger.Exit) err($"{field} exit 触发不能使用 goto");
                    if (string.IsNullOrEmpty(s.action)) err($"{field} goto 缺少 action");
                    break;
                case ScriptOp.SetVar:
                case ScriptOp.AddVar:
                    if (string.IsNullOrEmpty(s.name)) err($"{field} {op} 缺少变量名 name");
                    break;
                case ScriptOp.SkillStage:
                    if (string.IsNullOrEmpty(s.skill)) err($"{field} skillStage 缺少 skill");
                    if (string.IsNullOrEmpty(s.action)) err($"{field} skillStage 缺少 action");
                    CheckNumber(s.window, $"{field}.window", err);
                    CheckNumber(s.cdReturn, $"{field}.cdReturn", err);
                    if (!decimal.TryParse(s.window, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal window) || window <= 0m)
                        err($"{field}.window = \"{s.window}\" 必须大于 0");
                    break;
                case ScriptOp.SkillEnd:
                    if (string.IsNullOrEmpty(s.skill)) err($"{field} skillEnd 缺少 skill");
                    CheckNumber(s.cdReturn, $"{field}.cdReturn", err);
                    break;
                case ScriptOp.AddBuff:
                case ScriptOp.RemoveBuff:
                    if (string.IsNullOrEmpty(s.buff)) err($"{field} {op} 缺少 buff");
                    if (s.toTarget && trigger != ScriptTrigger.Hit) err($"{field} toTarget 只能用于 hit 触发 (作用于被打中的目标)");
                    if (op == ScriptOp.AddBuff) CheckNumber(s.window, $"{field}.window", err);
                    break;
            }
        }

        private static void CheckEnum<T>(string value, string field, Action<string> err) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!Enum.TryParse(value, true, out T _)) err($"{field} = \"{value}\" 无法识别，可选值: {string.Join(" / ", Enum.GetNames(typeof(T)))}");
        }

        private static void CheckTempTags(ActionData a, string[] ids, string field, Action<string> err)
        {
            if (ids == null) return;
            foreach (string id in ids)
            {
                bool found = false;
                if (a.tempBeCancelledTag != null)
                {
                    foreach (TempBeCancelledTag t in a.tempBeCancelledTag) found |= t.id == id;
                }
                if (!found) err($"{field} 引用了本动作未定义的 tempBeCancelledTag \"{id}\"");
            }
        }

        /// <summary>
        /// Camera 事件：指令名、参数数字格式与常见误配
        /// </summary>
        private static void CheckCamera(ActionEventData e, int ev, Action<string> err, Action<string> warn)
        {
            if (!CameraEffects.IsKnown(e.asset)) err($"events[{ev}] 未知镜头指令 \"{e.asset}\" (impact / impact_heavy / shot / shake / pulse / reset)");
            CameraEventData c = e.camera;
            string f = $"events[{ev}].camera";
            bool valid = true;
            void Bad(string message)
            {
                valid = false;
                err(message);
            }
            CheckNumber(c.zoom, $"{f}.zoom", Bad);
            CheckNumber(c.roll, $"{f}.roll", Bad);
            CheckNumber(c.inSec, $"{f}.inSec", Bad);
            CheckNumber(c.holdSec, $"{f}.holdSec", Bad);
            CheckNumber(c.outSec, $"{f}.outSec", Bad);
            CheckNumber(c.amplitude, $"{f}.amplitude", Bad);
            CheckNumber(c.frequency, $"{f}.frequency", Bad);
            CheckNumber(c.duration, $"{f}.duration", Bad);
            CheckNumber(c.interval, $"{f}.interval", Bad);
            CheckEnum<CameraEase>(c.ease, $"{f}.ease", err);
            if (!valid) return;

            CameraEventInfo p = c.ToRuntime(e.asset);
            float zoom = p.zoom.AsFloat();
            if ((e.asset == CameraEffects.Shot || e.asset == CameraEffects.Pulse) && (zoom < CameraControllor.MinZoom || zoom > CameraControllor.MaxZoomOut))
                warn($"{f}.zoom = {zoom} 超出镜头范围 [{CameraControllor.MinZoom}, {CameraControllor.MaxZoomOut}]，会被限制");
            if (e.asset == CameraEffects.Shot && Mathf.Abs(p.roll.AsFloat()) > CameraControllor.MaxRoll)
                warn($"{f}.roll 超过 {CameraControllor.MaxRoll} 度，会被限制");
            if (e.asset == CameraEffects.Shot && !p.holdUntilActionEnd && !e.stopOnActionEnd)
                warn($"events[{ev}] shot 配置了 holdSec 且未开 stopOnActionEnd：施法者换动作后镜头仍会保持到 holdSec 结束 (被打 / 被抓仍会复位)");
        }

        private static void CheckNumber(string value, string field, Action<string> err)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) err($"{field} = \"{value}\" 不是数字");
        }

        /// <returns>有效按键数</returns>
        private static int CheckKeys(string[] names, bool allowStick, string field, Action<string> err)
        {
            if (names == null) return 0;
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || !Enum.TryParse(name.Trim(), true, out CommandKey key) || !Enum.IsDefined(typeof(CommandKey), key) || key == CommandKey.None)
                {
                    err($"{field} 无法识别的按键 \"{name}\"");
                }
                else if (!allowStick && InputToCommand.IsStickKey(key))
                {
                    err($"{field} 摇杆键 {key} 只能写在 hold 中");
                }
            }
            return names.Length;
        }

        private void DrawValidation()
        {
            if (_validation == null) return;

            int errors = 0;
            foreach (Issue issue in _validation)
            {
                if (issue.Error) errors++;
            }

            string title = _validation.Count == 0 ? "校验通过" : $"校验结果：{errors} 个错误，{_validation.Count - errors} 个警告";
            _validationFold = EditorGUILayout.Foldout(_validationFold, title, true);
            if (!_validationFold || _validation.Count == 0) return;

            _validationScroll = EditorGUILayout.BeginScrollView(_validationScroll, GUILayout.MaxHeight(130));
            foreach (Issue issue in _validation)
            {
                GUI.color = issue.Error ? new Color(1f, 0.6f, 0.6f) : new Color(1f, 0.9f, 0.55f);
                if (GUILayout.Button(issue.Text, EditorStyles.label)) SelectAction(issue.Action);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndScrollView();
        }
    }
}
