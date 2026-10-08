using System;
using TrueSync;
using KiHan.Math;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KiHan.Tests
{
    /// <summary>
    /// 定点数确定性与数学精度全量自动化测试套件
    /// </summary>
    public static class FixedMathTests
    {
#if UNITY_EDITOR
        [MenuItem("KiHan/Math/Run Fixed Math Tests (定点数全量测试)")]
        public static void RunTestsMenu()
        {
            bool passed = RunAllTests();
            if (passed)
            {
                EditorUtility.DisplayDialog("KiHan Math Test", "所有定点数数学测试已全部通过！(100% Deterministic)", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("KiHan Math Test", "定点数数学测试存在失败项，请查看控制台日志！", "确定");
            }
        }
#endif

        public static bool RunAllTests()
        {
            Debug.Log("<color=cyan>====== [KiHan Fixed Math] 开始执行定点数全量测试 ======</color>");
            int passCount = 0;
            int totalCount = 0;

            Test("1. 基础乘除法精度 (Multiplication & Division)", () =>
            {
                FP a = (FP)2.5f;
                FP b = (FP)4.0f;
                FP mul = a * b;
                AssertTrue(Mathf.Abs(mul.ToFloat() - 10.0f) < 0.0001f, $"2.5 * 4.0 应为 10.0，实际: {mul.ToFloat()}");

                FP d = (FP)10.0f;
                FP e = (FP)2.5f;
                FP div = d / e;
                AssertTrue(Mathf.Abs(div.ToFloat() - 4.0f) < 0.0001f, $"10.0 / 2.5 应为 4.0，实际: {div.ToFloat()}");
            }, ref passCount, ref totalCount);

            Test("2. 负数符号与溢出边界 (Negative & Signs)", () =>
            {
                FP negA = (FP)(-3.5f);
                FP posB = (FP)2.0f;
                FP mul = negA * posB;
                AssertTrue(Mathf.Abs(mul.ToFloat() - (-7.0f)) < 0.0001f, $"-3.5 * 2.0 应为 -7.0，实际: {mul.ToFloat()}");

                FP div = mul / (FP)(-2.0f);
                AssertTrue(Mathf.Abs(div.ToFloat() - 3.5f) < 0.0001f, $"-7.0 / -2.0 应为 3.5，实际: {div.ToFloat()}");

                FP absVal = FPMath.Abs(negA);
                AssertTrue(absVal == (FP)3.5f, "Abs(-3.5) 应为 3.5");
            }, ref passCount, ref totalCount);

            Test("3. 查表三角函数 (Sin, Cos, Tan)", () =>
            {
                FP sin0 = FPMath.Sin(FP.Zero);
                FP sin30 = FPMath.Sin(FP.Deg2Rad * 30);
                FP sin90 = FPMath.Sin(FP.HalfPi);
                FP cos0 = FPMath.Cos(FP.Zero);
                FP cos60 = FPMath.Cos(FP.Deg2Rad * 60);

                AssertTrue(Mathf.Abs(sin0.ToFloat() - 0f) < 0.0001f, "Sin(0) 应为 0");
                AssertTrue(Mathf.Abs(sin30.ToFloat() - 0.5f) < 0.005f, $"Sin(30°) 应为 ~0.5，实际: {sin30.ToFloat()}");
                AssertTrue(Mathf.Abs(sin90.ToFloat() - 1.0f) < 0.0001f, $"Sin(90°) 应为 1.0，实际: {sin90.ToFloat()}");
                AssertTrue(Mathf.Abs(cos0.ToFloat() - 1.0f) < 0.0001f, "Cos(0) 应为 1.0");
                AssertTrue(Mathf.Abs(cos60.ToFloat() - 0.5f) < 0.005f, $"Cos(60°) 应为 ~0.5，实际: {cos60.ToFloat()}");
            }, ref passCount, ref totalCount);

            Test("4. 牛顿迭代开方 (Sqrt)", () =>
            {
                FP sqrt4 = FPMath.Sqrt((FP)4.0f);
                FP sqrt9 = FPMath.Sqrt((FP)9.0f);
                FP sqrt2 = FPMath.Sqrt((FP)2.0f);
                FP sqrt100 = FPMath.Sqrt((FP)100.0f);

                AssertTrue(Mathf.Abs(sqrt4.ToFloat() - 2.0f) < 0.0001f, "Sqrt(4) 应为 2");
                AssertTrue(Mathf.Abs(sqrt9.ToFloat() - 3.0f) < 0.0001f, "Sqrt(9) 应为 3");
                AssertTrue(Mathf.Abs(sqrt2.ToFloat() - 1.414214f) < 0.0001f, "Sqrt(2) 应为 1.414214");
                AssertTrue(Mathf.Abs(sqrt100.ToFloat() - 10.0f) < 0.0001f, "Sqrt(100) 应为 10");
            }, ref passCount, ref totalCount);

            Test("5. Atan2 角度换算 (Atan2)", () =>
            {
                FP angle45 = FPMath.Atan2((FP)1.0f, (FP)1.0f) * FPMath.Rad2Deg;
                FP angle90 = FPMath.Atan2((FP)1.0f, (FP)0.0f) * FPMath.Rad2Deg;
                FP angle180 = FPMath.Atan2((FP)0.0f, (FP)(-1.0f)) * FPMath.Rad2Deg;

                AssertTrue(Mathf.Abs(angle45.ToFloat() - 45.0f) < 0.5f, $"Atan2(1,1) 应为 ~45°，实际: {angle45.ToFloat()}");
                AssertTrue(Mathf.Abs(angle90.ToFloat() - 90.0f) < 0.5f, $"Atan2(1,0) 应为 ~90°，实际: {angle90.ToFloat()}");
                AssertTrue(Mathf.Abs(angle180.ToFloat() - 180.0f) < 0.5f, $"Atan2(0,-1) 应为 ~180°，实际: {angle180.ToFloat()}");
            }, ref passCount, ref totalCount);

            Test("6. FPVector2 二维向量与 3D 纵深投影", () =>
            {
                FPVector2 v = new FPVector2(3, 4);
                AssertTrue(Mathf.Abs(v.magnitude.ToFloat() - 5.0f) < 0.0001f, "Vector2(3,4) 模长应为 5");

                FPVector2 norm = v.normalized;
                AssertTrue(Mathf.Abs(norm.magnitude.ToFloat() - 1.0f) < 0.0001f, "归一化后模长应为 1");

                // 测试 3D 纵深渲染投影 (X -> X, Height -> Y, Y(纵深) -> Z)
                Vector3 worldPos = v.ToRenderWorldPos(heightMeters: 1.5f);
                AssertTrue(Mathf.Abs(worldPos.x - 3.0f) < 0.0001f, "WorldPos.x 应为 3.0");
                AssertTrue(Mathf.Abs(worldPos.y - 1.5f) < 0.0001f, "WorldPos.y (高度) 应为 1.5");
                AssertTrue(Mathf.Abs(worldPos.z - 4.0f) < 0.0001f, "WorldPos.z (纵深) 应为 4.0");
            }, ref passCount, ref totalCount);

            Test("7. FPVector3 三维向量叉乘与点乘", () =>
            {
                FPVector3 up = FPVector3.up;
                FPVector3 right = FPVector3.right;
                FPVector3 forward = FPVector3.Cross(right, up);
                AssertTrue(forward == FPVector3.forward, "right x up 应等于 forward");

                FP dot = FPVector3.Dot(up, right);
                AssertTrue(dot == FP.Zero, "垂直向量点乘应等于 0");
            }, ref passCount, ref totalCount);

            Test("8. 100,000 步动力学模拟哈希确定性", () =>
            {
                FP pos = FP.Zero;
                FP vel = (FP)10.0f;
                FP gravity = (FP)0.5f;

                for (int i = 0; i < 100000; i++)
                {
                    pos += vel;
                    vel -= gravity;
                    if (pos < FP.Zero)
                    {
                        pos = -pos * (FP)0.8f;
                        vel = -vel * (FP)0.8f;
                    }
                }

                // 确定的 RawValue 结果
                const long EXPECTED_HASH = 78793090073L;
                AssertTrue(pos.RawValue == EXPECTED_HASH, $"100,000 步模拟校验和不一致！期望: {EXPECTED_HASH}, 实际: {pos.RawValue}");
            }, ref passCount, ref totalCount);

            Debug.Log($"<color={(passCount == totalCount ? "green" : "red")}>====== [KiHan Fixed Math] 测试完成: {passCount}/{totalCount} 通过 ======</color>");
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
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
