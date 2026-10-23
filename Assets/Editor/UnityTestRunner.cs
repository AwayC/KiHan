using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using KiHan.Logic;
using KiHan.Tests;

namespace KiHan.Editor
{
    [InitializeOnLoad]
    public static class UnityTestRunner
    {
        private static bool _hasRun = false;

        static UnityTestRunner()
        {
            // 在 Unity 编译完成并进行域重新加载后延时 1 秒自动触发
            EditorApplication.delayCall += () =>
            {
                if (!_hasRun)
                {
                    _hasRun = true;
                    RunAllUnityEnvironmentTests();
                }
            };
        }

        [MenuItem("KiHan/Testing/Run All Unity Environment Tests (运行Unity环境全量测试)")]
        public static void RunAllUnityEnvironmentTests()
        {
            List<string> logs = new List<string>();
            bool allPassed = true;

            void Log(string msg)
            {
                logs.Add(msg);
                Debug.Log(msg);
            }

            void LogError(string msg)
            {
                allPassed = false;
                logs.Add("[FAIL] " + msg);
                Debug.LogError(msg);
            }

            Log("========================================================================");
            Log(">>> KiHan Unity 真实环境全量测试套件开始运行 <<<");
            Log($"Unity 版本: {Application.unityVersion}, 平台: {Application.platform}");
            Log($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log("========================================================================");

            // 1. 定点数数学测试 (FixedMathTests)
            try
            {
                Log("\n--- [1/5] 定点数确定性与数学库测试 (FixedMathTests) ---");
                bool mathOk = FixedMathTests.RunAllTests();
                if (mathOk)
                {
                    Log("  [OK] FixedMathTests 全部定点数数学测试通过");
                }
                else
                {
                    LogError("FixedMathTests 测试未完全通过！");
                }
            }
            catch (Exception ex)
            {
                LogError($"FixedMathTests 抛出未捕获异常: {ex.Message}\n{ex.StackTrace}");
            }

            // 2. 资源管理系统与分级对象池测试 (AssetTests)
            try
            {
                Log("\n--- [2/5] 资源管理与对象池测试 (AssetTests) ---");
                bool assetOk = AssetTests.RunAllTests();
                if (assetOk)
                {
                    Log("  [OK] AssetTests 资源系统与对象池测试全部通过");
                }
                else
                {
                    LogError("AssetTests 测试未完全通过！");
                }
            }
            catch (Exception ex)
            {
                LogError($"AssetTests 抛出未捕获异常: {ex.Message}\n{ex.StackTrace}");
            }

            // 3. Unity JsonUtility 配置加载复核 (.agents/testing.md L2.3)
            try
            {
                Log("\n--- [3/5] Unity JsonUtility 配置加载测试 (.agents/testing.md L2.3) ---");

                // 3.1 验证 action_template.json
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "action_template.json");
                if (!File.Exists(templatePath))
                {
                    LogError($"action_template.json 文件不存在: {templatePath}");
                }
                else
                {
                    string templateJson = File.ReadAllText(templatePath);
                    var templateContainer = JsonUtility.FromJson<ActionDataContainer>(templateJson);
                    if (templateContainer == null || templateContainer.data == null)
                    {
                        LogError("JsonUtility.FromJson<ActionDataContainer>(action_template.json) 返回空数据！");
                    }
                    else
                    {
                        var acts = new List<ActionInfo>();
                        foreach (var d in templateContainer.data)
                        {
                            acts.Add(d.ToRuntime());
                        }

                        var missingHurt = HurtActionIds.FindMissing(acts);
                        if (acts.Count == 9 && missingHurt.Count == 0)
                        {
                            Log($"  [OK] action_template.json: 成功解析 {acts.Count} 个动作，通用受击动作齐全 (FindMissing 为空)");
                        }
                        else
                        {
                            LogError($"action_template.json 动作数量或受击动作不匹配: 动作数={acts.Count} (预期9), 缺失受击=[{string.Join(",", missingHurt)}]");
                        }
                    }
                }

                // 3.2 验证 example.json (剔除 // 注释后)
                string examplePath = Path.Combine(Directory.GetCurrentDirectory(), "example.json");
                if (!File.Exists(examplePath))
                {
                    LogError($"example.json 文件不存在: {examplePath}");
                }
                else
                {
                    string rawExample = File.ReadAllText(examplePath);
                    // 剔除所有 // 注释（包括行内末尾注释）与可能多余的闭合逗号
                    string cleanedJson = Regex.Replace(rawExample, @"//.*$", "", RegexOptions.Multiline);
                    cleanedJson = Regex.Replace(cleanedJson, @",\s*([}\]])", "$1");
                    var exampleContainer = JsonUtility.FromJson<ActionDataContainer>(cleanedJson);
                    if (exampleContainer == null || exampleContainer.data == null)
                    {
                        LogError("JsonUtility 解析 example.json (清理注释后) 返回空！");
                    }
                    else
                    {
                        int exampleCount = 0;
                        var exampleProblems = new List<string>();
                        foreach (var d in exampleContainer.data)
                        {
                            ValidateCommands(d.ToRuntime(), exampleProblems);
                            exampleCount++;
                        }
                        foreach (string p in exampleProblems) LogError($"example.json 配置错误: {p}");
                        if (exampleProblems.Count == 0) Log($"  [OK] example.json: JsonUtility 成功解析并转换为 Runtime 动作，共 {exampleCount} 个动作配置");
                    }
                }

                // 3.3 验证 skills_example.json / spawns_example.json (剔除注释后)
                var skillExample = JsonUtility.FromJson<SkillDataContainer>(ReadCommentedJson("skills_example.json"));
                var spawnExample = JsonUtility.FromJson<SpawnDataContainer>(ReadCommentedJson("spawns_example.json"));
                if (skillExample?.data == null || spawnExample?.data == null)
                {
                    LogError("JsonUtility 解析 skills_example.json / spawns_example.json (清理注释后) 返回空！");
                }
                else
                {
                    foreach (var d in skillExample.data) d.ToRuntime();
                    foreach (var d in spawnExample.data) d.ToRuntime();
                    Log($"  [OK] skills_example.json: {skillExample.data.Length} 个技能; spawns_example.json: {spawnExample.data.Length} 个生成物");
                }
            }
            catch (Exception ex)
            {
                LogError($"JsonUtility 配置加载测试异常: {ex.Message}\n{ex.StackTrace}");
            }

            // 4. 帧库与判定盒资产检查 (.agents/testing.md L2.2)
            try
            {
                Log("\n--- [4/5] 帧库与判定盒资产检查 (NarutoFrameLib.asset) ---");
                string libPath = "Assets/AssetPackages/Resources/Characters/naruto/NarutoFrameLib.asset";
                var frameLib = AssetDatabase.LoadAssetAtPath<CharacterSpriteLibrary>(libPath);
                if (frameLib == null)
                {
                    LogError($"无法通过 AssetDatabase 加载帧库: {libPath}");
                }
                else
                {
                    int frameCount = frameLib.AllFrames != null ? frameLib.AllFrames.Count : 0;
                    Log($"  [OK] 帧库加载成功，共 {frameCount} 帧数据");
                    if (frameCount > 0)
                    {
                        var firstFrame = frameLib.AllFrames[0];
                        int hurtCount = firstFrame.HurtBoxes != null ? firstFrame.HurtBoxes.Count : 0;
                        int hitCount = firstFrame.HitBoxes != null ? firstFrame.HitBoxes.Count : 0;
                        Log($"  [OK] 帧数据健全: 采样第0帧 HurtBoxes={hurtCount}, HitBoxes={hitCount}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"frameLib.asset 检查异常: {ex.Message}\n{ex.StackTrace}");
            }

            // 5. 角色配置交叉引用 (动作 / 技能 / 生成物 / 动作事件 / 动画 / 特效资源)
            try
            {
                Log("\n--- [5/5] 角色配置交叉引用检查 (actions.json / skills.json / spawns.json) ---");
                const int narutoId = 90001;
                CharacterFactory.ClearTemplates();
                CharacterTemplate template = CharacterFactory.GetTemplate(narutoId);
                List<string> problems = ValidateTemplate(template);
                CharacterFactory.ClearTemplates();
                if (problems.Count == 0)
                {
                    Log($"  [OK] 角色 {narutoId}: 动作 {template.Actions.Count} 个、技能 {template.Skills.Length} 个、生成物 {template.Spawns.Count} 个，引用全部有效");
                }
                else
                {
                    foreach (string p in problems) LogError($"角色 {narutoId} 配置错误: {p}");
                }
            }
            catch (Exception ex)
            {
                LogError($"角色配置检查异常: {ex.Message}\n{ex.StackTrace}");
            }

            // 总结报告
            Log("\n========================================================================");
            if (allPassed)
            {
                Log(">>> [RESULT: ALL PASSED] Unity 真实环境所有测试与验证全部通过！ <<<");
            }
            else
            {
                Log(">>> [RESULT: SOME TESTS FAILED] Unity 真实环境测试存在失败项！ <<<");
            }
            Log("========================================================================");

            // 写入本地日志文件方便终端查看
            try
            {
                string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "unity_test_results.log");
                File.WriteAllLines(reportPath, logs);
            }
            catch { }
        }

        /// <summary>
        /// 读取仓库根目录下的带注释 JSON：剔除 // 注释与多余的闭合逗号
        /// </summary>
        private static string ReadCommentedJson(string fileName)
        {
            string raw = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), fileName));
            string cleaned = Regex.Replace(raw, @"//.*$", "", RegexOptions.Multiline);
            return Regex.Replace(cleaned, @",\s*([}\]])", "$1");
        }

        /// <summary>
        /// 触发指令：每条至少一个按键，按键名全部可识别 (无法识别或摇杆键写在 press / release 中时 ToRuntime 记为 None)
        /// </summary>
        private static void ValidateCommands(ActionInfo a, List<string> problems)
        {
            if (a.commands == null) return;
            for (int i = 0; i < a.commands.Length; i++)
            {
                ActionCommand c = a.commands[i];
                int count = c.press.Length + c.hold.Length + c.release.Length;
                if (count == 0) problems.Add($"动作 {a.id} 的第 {i} 条指令没有配置任何按键");
                if (Array.IndexOf(c.press, CommandKey.None) >= 0 || Array.IndexOf(c.hold, CommandKey.None) >= 0 || Array.IndexOf(c.release, CommandKey.None) >= 0)
                    problems.Add($"动作 {a.id} 的第 {i} 条指令含无法识别的按键 (摇杆键只能写在 hold 中)");
            }
        }

        /// <summary>
        /// 角色配置交叉引用：动作的 animKey / autoNext / 命中动作变化目标、技能段 (入口与后续) 动作、生成物初始与消失动作、
        /// 动作事件引用的生成物与特效资源都必须存在
        /// </summary>
        private static List<string> ValidateTemplate(CharacterTemplate template)
        {
            var problems = new List<string>();
            if (template == null)
            {
                problems.Add("模板加载失败");
                return problems;
            }

            var actionIds = new HashSet<string>();
            foreach (ActionInfo a in template.Actions) actionIds.Add(a.id);
            var animKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimationFrameData clip in template.Anims) animKeys.Add(clip.name);

            void RequireAction(string id, string where)
            {
                if (!string.IsNullOrEmpty(id) && !actionIds.Contains(id)) problems.Add($"{where} 引用了不存在的动作 {id}");
            }

            void RequireIcon(string address, string where)
            {
                if (!string.IsNullOrEmpty(address) && global::View.UI.SkillIconCache.Load(address) == null) problems.Add($"{where} 的图标不存在: {address}");
            }

            foreach (ActionInfo a in template.Actions)
            {
                if (!animKeys.Contains(a.animKey)) problems.Add($"动作 {a.id} 的动画 {a.animKey} 不存在");
                RequireAction(a.autoNextActionId, $"动作 {a.id} 的 autoNextActionId");
                ValidateCommands(a, problems);
                if (a.attacks != null)
                {
                    foreach (AttackInfo atk in a.attacks)
                    {
                        if (atk.selfActionChange.changeType == ActionChangeType.ChangeToActionId) RequireAction(atk.selfActionChange.param, $"动作 {a.id} 的 selfActionChange");
                        RequireAction(atk.hurtAction, $"动作 {a.id} 的 hurtAction");
                    }
                }
                if (a.events == null) continue;
                foreach (ActionEventInfo evt in a.events)
                {
                    if (evt.type == ActionEventType.Spawn && !template.Spawns.ContainsKey(evt.spawnId))
                        problems.Add($"动作 {a.id} 第 {evt.frame} 帧生成了未配置的生成物 {evt.spawnId}");
                    if (evt.type == ActionEventType.Effect && Resources.Load<GameObject>(evt.asset) == null)
                        problems.Add($"动作 {a.id} 第 {evt.frame} 帧的特效资源不存在: {evt.asset}");
                    if (evt.type == ActionEventType.Buff && !template.Buffs.ContainsKey(evt.asset))
                        problems.Add($"动作 {a.id} 第 {evt.frame} 帧添加了未配置的 Buff {evt.asset}");
                }
            }

            foreach (SkillInfo skill in template.Skills)
            {
                if (skill.stages.Length == 0) problems.Add($"技能 {skill.id} 没有配置段");
                RequireIcon(skill.icon, $"技能 {skill.id}");
                foreach (SkillStageInfo stage in skill.stages)
                {
                    RequireAction(stage.actionId, $"技能 {skill.id}");
                    foreach (string follow in stage.followActions) RequireAction(follow, $"技能 {skill.id} 的后续动作");
                    RequireIcon(stage.icon, $"技能 {skill.id} 的段 {stage.actionId}");
                }
            }

            foreach (BuffInfo buff in template.Buffs.Values)
            {
                if (Resources.Load<GameObject>(buff.effect) == null) problems.Add($"Buff {buff.id} 的特效资源不存在: {buff.effect}");
            }

            foreach (SpawnInfo spawn in template.Spawns.Values)
            {
                RequireAction(spawn.initialActionId, $"生成物 {spawn.id} 的初始动作");
                RequireAction(spawn.despawnActionId, $"生成物 {spawn.id} 的消失动作");
            }
            return problems;
        }
    }
}
