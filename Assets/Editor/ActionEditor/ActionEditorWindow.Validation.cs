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
                        if (type == ActionEventType.Spawn && string.IsNullOrEmpty(e.spawnId)) Err($"events[{ev}] Spawn 事件缺少 spawnId");
                        if (type != ActionEventType.Spawn && string.IsNullOrEmpty(e.asset)) Err($"events[{ev}] {type} 事件缺少 asset");
                        if (e.loop && !e.bind) Warn($"events[{ev}] 循环特效未绑定角色 (bind)");
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
