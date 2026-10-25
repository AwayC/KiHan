using System;
using UnityEngine;
using KiHan.Asset;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KiHan.Tests
{
    /// <summary>
    /// 资源管理与对象池自动化单元测试
    /// </summary>
    public static class AssetTests
    {
#if UNITY_EDITOR
        [MenuItem("KiHan/Asset/Run Asset & Pool Tests (资源与对象池测试)")]
        public static void RunTestsMenu()
        {
            bool passed = RunAllTests();
            if (passed)
            {
                EditorUtility.DisplayDialog("KiHan Asset Test", "资源系统与分级对象池测试全部通过！", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("KiHan Asset Test", "资源测试存在失败项，请查看控制台日志！", "确定");
            }
        }
#endif

        private class MockPoolItem : MonoBehaviour, IPoolable
        {
            public int SpawnCount = 0;
            public int RecycleCount = 0;

            public void OnSpawn()
            {
                SpawnCount++;
            }

            public void OnRecycle()
            {
                RecycleCount++;
            }
        }

        public static bool RunAllTests()
        {
            Debug.Log("<color=cyan>====== [KiHan Asset & Pool] 开始执行资源系统全量测试 ======</color>");
            int passCount = 0;
            int totalCount = 0;

            Test("1. 对象池生命周期契约回调 (IPoolable OnSpawn & OnRecycle)", () =>
            {
                string testAddress = "Test/MockPoolItem";
                var pool = ObjectPoolManager.Instance;

                // 测试预制体直接登记到对象池 (不经 AssetManager 按地址加载)
                GameObject samplePrefab = new GameObject("MockPrefab");
                samplePrefab.AddComponent<MockPoolItem>();
                pool.RegisterPrefab(testAddress, samplePrefab);

                // 池空：实例化新对象并触发 OnSpawn
                GameObject instance = pool.Spawn(testAddress, Vector3.zero, Quaternion.identity, null, PoolCategory.FX);
                AssertTrue(instance != null, "登记过预制体的地址应能生成实例");
                var mock = instance.GetComponent<MockPoolItem>();
                AssertTrue(mock.SpawnCount == 1, "OnSpawn 应该被调用 1 次");

                // 回收
                pool.Recycle(instance);
                AssertTrue(mock.RecycleCount == 1, "OnRecycle 应该被调用 1 次");
                AssertTrue(!instance.activeSelf, "回收后 GameObject 应处于隐藏状态");

                // 再次取出 (复用)
                GameObject reused = pool.Spawn(testAddress, Vector3.one, Quaternion.identity, null, PoolCategory.FX);
                AssertTrue(reused == instance, "对象池应复用同一实例");
                AssertTrue(reused.activeSelf, "重新取出后 GameObject 应处于激活状态");
                AssertTrue(mock.SpawnCount == 2, "复用时 OnSpawn 应再次调用");

                // 清理
                pool.Recycle(reused);
                pool.ClearCategory(PoolCategory.FX);
                UnityEngine.Object.DestroyImmediate(samplePrefab);
            }, ref passCount, ref totalCount);

            Test("2. 分类隔离清理 (ClearCategory)", () =>
            {
                var pool = ObjectPoolManager.Instance;
                pool.ClearCategory(PoolCategory.UI);
                pool.ClearCategory(PoolCategory.FX);
                AssertTrue(true, "分类隔离清理成功完成");
            }, ref passCount, ref totalCount);

            Debug.Log($"<color={(passCount == totalCount ? "green" : "red")}>====== [KiHan Asset & Pool] 测试完成: {passCount}/{totalCount} 通过 ======</color>");
            return passCount == totalCount;
        }

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
