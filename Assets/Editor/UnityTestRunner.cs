using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using KiHan.Config;
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
                            CharacterValidator.ValidateCommands(d.ToRuntime(), exampleProblems);
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

            // 4. 帧库与判定盒资产检查 (.agents/testing.md L2.2)：注册表中每个角色包的帧库
            // 5. 角色配置交叉引用 (动作 / 技能 / 生成物 / 动作事件 / 动画 / 包内资源)
            try
            {
                Log("\n--- [4/5] 帧库与判定盒资产检查 / [5/5] 角色包配置交叉引用检查 (注册表全部角色) ---");
                CharacterRegistry registry = CharacterPackages.LoadRegistry();
                if (registry == null)
                {
                    LogError("找不到角色注册表 Resources/Config/CharacterRegistry.asset");
                }
                else
                {
                    foreach (CharacterRegistryEntry entry in registry.characters)
                    {
                        CharacterManifest manifest = entry.manifest;
                        if (manifest == null)
                        {
                            LogError("注册表中有丢失的角色包");
                            continue;
                        }

                        CharacterSpriteLibrary frameLib = null;
                        foreach (AnimationFrameData clip in manifest.animations)
                        {
                            if (clip != null && clip.Library != null) { frameLib = clip.Library; break; }
                        }
                        int frameCount = frameLib != null && frameLib.AllFrames != null ? frameLib.AllFrames.Count : 0;
                        if (frameCount == 0) LogError($"角色 {manifest.id} 的动画没有引用帧库或帧库为空");
                        else Log($"  [OK] 角色 {manifest.id} 帧库 {frameLib.name}: {frameCount} 帧");

                        List<string> problems = CharacterValidator.Validate(manifest);
                        if (problems.Count == 0)
                        {
                            Log($"  [OK] 角色 {manifest.id} {manifest.displayName}{(entry.enabled ? string.Empty : " (未启用)")}: 配置引用全部有效");
                        }
                        else
                        {
                            foreach (string p in problems) LogError($"角色 {manifest.id} 配置错误: {p}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"角色包检查异常: {ex.Message}\n{ex.StackTrace}");
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
    }
}
