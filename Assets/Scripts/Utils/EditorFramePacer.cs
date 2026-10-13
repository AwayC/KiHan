#if UNITY_EDITOR
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

/// <summary>
/// 编辑器专用锁帧，替代 Application.targetFrameRate。
/// 编辑器下 targetFrameRate 的等待期间 EditorApplication.update 会空转 (实测 30fps 时约 1700 次/帧)，
/// Device Simulator 的旧输入后端每次空转都补发一个 Stationary 触摸，原生触摸队列因此积压，
/// 松手的 Ended 晚约 10 帧才到达 (30fps 下约 333ms)，表现为虚拟按键/摇杆"按下后很久才释放"。
/// 这里在 PlayerLoop 最前端阻塞主线程等到帧截止时间：等待期间编辑器不空转，且输入在等待结束后才轮询，不增加输入延迟。
/// </summary>
public static class EditorFramePacer
{
    private struct Wait { }

    private static readonly Stopwatch Clock = new Stopwatch();
    private static long _periodTicks;
    private static long _deadline;
    private static bool _installed;

    public static void Install(int frameRate)
    {
        _periodTicks = Stopwatch.Frequency / frameRate;
        Application.targetFrameRate = -1;
        if (_installed) return;

        var root = PlayerLoop.GetCurrentPlayerLoop();
        var phases = root.subSystemList;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i].type != typeof(Initialization)) continue;
            var old = phases[i].subSystemList;
            var list = new PlayerLoopSystem[old.Length + 1];
            list[0] = new PlayerLoopSystem { type = typeof(Wait), updateDelegate = WaitForDeadline };
            old.CopyTo(list, 1);
            phases[i].subSystemList = list;
            break;
        }
        PlayerLoop.SetPlayerLoop(root);

        Clock.Restart();
        _deadline = 0;
        _installed = true;
        Application.quitting += Uninstall;
    }

    private static void Uninstall()
    {
        Application.quitting -= Uninstall;
        if (!_installed) return;
        _installed = false;

        var root = PlayerLoop.GetCurrentPlayerLoop();
        var phases = root.subSystemList;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i].type != typeof(Initialization)) continue;
            phases[i].subSystemList = System.Array.FindAll(phases[i].subSystemList, s => s.type != typeof(Wait));
            break;
        }
        PlayerLoop.SetPlayerLoop(root);
    }

    private static void WaitForDeadline()
    {
        long now = Clock.ElapsedTicks;
        // 落后超过一帧 (卡顿/断点) 时重新对齐，不连续补帧
        if (now - _deadline > _periodTicks) _deadline = now;
        long sleepThreshold = Stopwatch.Frequency / 500; // 2ms 以上用 Sleep，余量自旋保证精度
        long remain;
        while ((remain = _deadline - Clock.ElapsedTicks) > 0)
        {
            if (remain > sleepThreshold) Thread.Sleep(1);
            else Thread.SpinWait(32);
        }
        _deadline += _periodTicks;
    }
}
#endif
