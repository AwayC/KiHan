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
                Log("\n--- [1/4] 定点数确定性与数学库测试 (FixedMathTests) ---");
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
                Log("\n--- [2/4] 资源管理与对象池测试 (AssetTests) ---");
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
                Log("\n--- [3/4] Unity JsonUtility 配置加载测试 (.agents/testing.md L2.3) ---");

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
                        foreach (var d in exampleContainer.data)
                        {
                            var act = d.ToRuntime();
                            exampleCount++;
                        }
                        Log($"  [OK] example.json: JsonUtility 成功解析并转换为 Runtime 动作，共 {exampleCount} 个动作配置");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"JsonUtility 配置加载测试异常: {ex.Message}\n{ex.StackTrace}");
            }

            // 4. 帧库与判定盒资产检查 (.agents/testing.md L2.2)
            try
            {
                Log("\n--- [4/4] 帧库与判定盒资产检查 (frameLib.asset) ---");
                string libPath = "Assets/AssetPackages/Resources/Characters/naruto/frameLib.asset";
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
    }
}
