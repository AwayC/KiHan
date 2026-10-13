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
                FP a = FP.Parse("2.5");
                FP b = 4;
                FP mul = a * b;
                AssertTrue(mul == 10, $"2.5 * 4 应为 10，实际: {mul}");

                FP d = 10;
                FP e = FP.Parse("2.5");
                FP div = d / e;
                AssertTrue(div == 4, $"10 / 2.5 应为 4，实际: {div}");
            }, ref passCount, ref totalCount);

            Test("2. 负数符号与溢出边界 (Negative & Signs)", () =>
            {
                FP negA = FP.Parse("-3.5");
                FP posB = 2;
                FP mul = negA * posB;
                AssertTrue(mul == -7, $"-3.5 * 2 应为 -7，实际: {mul}");

                FP div = mul / -2;
                AssertTrue(div == FP.Parse("3.5"), $"-7 / -2 应为 3.5，实际: {div}");

                FP absVal = FPMath.Abs(negA);
                AssertTrue(absVal == FP.Parse("3.5"), "Abs(-3.5) 应为 3.5");
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
                FP sqrt4 = FPMath.Sqrt(4);
                FP sqrt9 = FPMath.Sqrt(9);
                FP sqrt2 = FPMath.Sqrt(2);
                FP sqrt100 = FPMath.Sqrt(100);

                AssertTrue(Mathf.Abs(sqrt4.ToFloat() - 2.0f) < 0.0001f, "Sqrt(4) 应为 2");
                AssertTrue(Mathf.Abs(sqrt9.ToFloat() - 3.0f) < 0.0001f, "Sqrt(9) 应为 3");
                AssertTrue(Mathf.Abs(sqrt2.ToFloat() - 1.414214f) < 0.0001f, "Sqrt(2) 应为 1.414214");
                AssertTrue(Mathf.Abs(sqrt100.ToFloat() - 10.0f) < 0.0001f, "Sqrt(100) 应为 10");
            }, ref passCount, ref totalCount);

            Test("5. Atan2 角度换算 (Atan2)", () =>
            {
                FP angle45 = FPMath.Atan2(1, 1) * FPMath.Rad2Deg;
                FP angle90 = FPMath.Atan2(1, 0) * FPMath.Rad2Deg;
                FP angle180 = FPMath.Atan2(0, -1) * FPMath.Rad2Deg;

                AssertTrue(Mathf.Abs(angle45.ToFloat() - 45.0f) < 0.5f, $"Atan2(1,1) 应为 ~45°，实际: {angle45.ToFloat()}");
                AssertTrue(Mathf.Abs(angle90.ToFloat() - 90.0f) < 0.5f, $"Atan2(1,0) 应为 ~90°，实际: {angle90.ToFloat()}");
                AssertTrue(Mathf.Abs(angle180.ToFloat() - 180.0f) < 0.5f, $"Atan2(0,-1) 应为 ~180°，实际: {angle180.ToFloat()}");
            }, ref passCount, ref totalCount);

            Test("6. FPVector2 二维向量模长与归一化", () =>
            {
                FPVector2 v = new FPVector2(3, 4);
                AssertTrue(Mathf.Abs(v.magnitude.ToFloat() - 5.0f) < 0.0001f, "Vector2(3,4) 模长应为 5");

                FPVector2 norm = v.normalized;
                AssertTrue(Mathf.Abs(norm.magnitude.ToFloat() - 1.0f) < 0.0001f, "归一化后模长应为 1");
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
                FP vel = 10;
                FP gravity = FP.Half;
                FP bounceFactor = FP.Parse("0.8");

                for (int i = 0; i < 100000; i++)
                {
                    pos += vel;
                    vel -= gravity;
                    if (pos < FP.Zero)
                    {
                        pos = -pos * bounceFactor;
                        vel = -vel * bounceFactor;
                    }
                }

                // 基于 FP.Parse("0.8") 纯整型运算的 100% 确定性校验和
                const long EXPECTED_HASH = 78793089710L;
                AssertTrue(pos.RawValue == EXPECTED_HASH, $"100,000 步模拟校验和不一致！期望: {EXPECTED_HASH}, 实际: {pos.RawValue}");
            }, ref passCount, ref totalCount);

            Test("9. 字符串解析与纯整型 0 浮点确定性 (String-First Standard)", () =>
            {
                FP p1 = FP.Parse("0.15");
                FP p2 = FP.Parse("0.1");
                FP p3 = FP.Parse("0.05");
                FP p4 = FP.Parse("-12.345");

                // 验证二进制 Q32.32 rawValue 精确匹配
                AssertTrue(p1.RawValue == 644245094L, $"0.15 RawValue 应为 644245094，实际: {p1.RawValue}");
                AssertTrue(p2.RawValue == 429496729L, $"0.1 RawValue 应为 429496729，实际: {p2.RawValue}");
                AssertTrue(p3.RawValue == 214748364L, $"0.05 RawValue 应为 214748364，实际: {p3.RawValue}");
                AssertTrue(p4.RawValue == -53021371269L, $"-12.345 RawValue 应为 -53021371269，实际: {p4.RawValue}");

                // 验证隐式字符串转换与 Parse 严格一致
                FP implicitVal = "0.15";
                AssertTrue(implicitVal == p1, "隐式字符串赋值应与 FP.Parse 完全一致");
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
