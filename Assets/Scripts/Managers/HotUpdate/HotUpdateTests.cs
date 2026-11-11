using System;
using System.Collections.Generic;
using KiHan.Asset;
using KiHan.HotUpdate;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KiHan.Tests
{
    /// <summary>
    /// 热更判定测试 (HotUpdatePlan)
    /// </summary>
    public static class HotUpdateTests
    {
        private const string App = "0.1";

#if UNITY_EDITOR
        [MenuItem("KiHan/Asset/Run Hot Update Tests (热更判定测试)")]
        public static void RunTestsMenu()
        {
            bool passed = RunAllTests();
            EditorUtility.DisplayDialog("KiHan HotUpdate Test", passed ? "热更判定测试全部通过！" : "热更判定测试存在失败项，请查看控制台日志！", "确定");
        }
#endif

        public static bool RunAllTests()
        {
            Debug.Log("<color=cyan>====== [KiHan HotUpdate] 开始执行热更判定测试 ======</color>");
            int passCount = 0;
            int totalCount = 0;

            Test("1. UsePersistent：同整包版本且资源版本更高才用持久清单", () =>
            {
                BundleManifest builtin = Manifest(App, 3);
                bool higher = HotUpdatePlan.UsePersistent(builtin, Manifest(App, 4), App);
                AssertTrue(higher, $"持久 4 > 首包 3 应使用持久清单，实际 {higher}");
                bool same = HotUpdatePlan.UsePersistent(builtin, Manifest(App, 3), App);
                AssertTrue(!same, $"持久 3 = 首包 3 不应使用，实际 {same}");
                bool lower = HotUpdatePlan.UsePersistent(builtin, Manifest(App, 2), App);
                AssertTrue(!lower, $"持久 2 < 首包 3 不应使用，实际 {lower}");
                bool otherApp = HotUpdatePlan.UsePersistent(builtin, Manifest("0.2", 9), App);
                AssertTrue(!otherApp, $"整包版本不同 (0.2 vs {App}) 不应使用，实际 {otherApp}");
                bool none = HotUpdatePlan.UsePersistent(builtin, null, App);
                AssertTrue(!none, $"没有持久清单不应使用，实际 {none}");
            }, ref passCount, ref totalCount);

            Test("2. ShouldUpdate：同整包版本且远端资源版本更高才更新", () =>
            {
                BundleManifest local = Manifest(App, 3);
                bool equal = HotUpdatePlan.ShouldUpdate(local, Version(App, 3), App);
                AssertTrue(!equal, $"远端 3 = 本地 3 不应更新，实际 {equal}");
                bool higher = HotUpdatePlan.ShouldUpdate(local, Version(App, 4), App);
                AssertTrue(higher, $"远端 4 > 本地 3 应更新，实际 {higher}");
                bool otherApp = HotUpdatePlan.ShouldUpdate(local, Version("0.2", 4), App);
                AssertTrue(!otherApp, $"远端整包版本 0.2 ≠ {App} 不应更新，实际 {otherApp}");
            }, ref passCount, ref totalCount);

            Test("3. Missing：只返回本地与首包都没有的包", () =>
            {
                BundleManifest remote = Manifest(App, 4, "a_1.ab", "b_2.ab", "c_3.ab");
                var builtin = new HashSet<string> { "a_1.ab" };
                var persistent = new HashSet<string> { "b_2.ab" };
                List<BundleInfo> missing = HotUpdatePlan.Missing(remote, f => builtin.Contains(f) || persistent.Contains(f));
                string files = string.Join(",", missing.ConvertAll(b => b.file));
                AssertTrue(files == "c_3.ab", $"预期只缺 c_3.ab，实际 [{files}]");
            }, ref passCount, ref totalCount);

            Debug.Log($"<color={(passCount == totalCount ? "green" : "red")}>====== [KiHan HotUpdate] 测试完成: {passCount}/{totalCount} 通过 ======</color>");
            return passCount == totalCount;
        }

        private static BundleManifest Manifest(string app, int res, params string[] files)
        {
            var bundles = new BundleInfo[files.Length];
            for (int i = 0; i < files.Length; i++) bundles[i] = new BundleInfo { name = files[i], file = files[i], deps = Array.Empty<int>() };
            return new BundleManifest { appVersion = app, resVersion = res, bundles = bundles, assets = Array.Empty<AssetInfo>() };
        }

        private static VersionInfo Version(string app, int res) => new VersionInfo { appVersion = app, resVersion = res };

        private static void Test(string name, Action action, ref int passCount, ref int totalCount)
        {
            totalCount++;
            try
            {
                action();
                Debug.Log($"<color=green>[PASS]</color> {name}");
                passCount++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=red>[FAIL]</color> {name} -> 异常: {ex.Message}");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
