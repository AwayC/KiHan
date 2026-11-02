using System;
using System.Collections;
using KiHan.DebugTools;
using KiHan.UI;
using Managers;
using UnityEngine;

/// <summary>
/// 游戏流程：登录 → 大厅 → 战斗 → 大厅 / 登出。决定什么时候打开哪个界面、什么时候进出战斗；
/// 界面只调用这里的意图方法 (开始战斗、退出战斗、登出)，不直接调用 BattleManager 与网络清理
/// </summary>
public sealed class GameFlow : UnitySingleton<GameFlow>
{
    /// <summary>
    /// 战斗逻辑是否在运行 (BattleManager 只在运行时推进逻辑帧)
    /// </summary>
    public bool IsGameRunning { get; private set; }

    public void SetGameRunning(bool running) => IsGameRunning = running;

    /// <summary>
    /// 启动：调试配置直接进单机战斗，否则进登录
    /// </summary>
    public void Boot()
    {
        DebugConfig dbg = Resources.Load<DebugConfig>(DebugConfig.DefaultPath);
        if (dbg != null && dbg.isBattleDebug) StartOfflineBattle();
        else EnterLogin();
    }

    public void EnterLogin()
    {
        CloseScreens();
        UIManager.Instance.Open<LoginPanel>();
    }

    public void EnterLobby()
    {
        CloseScreens();
        UIManager.Instance.Open<LobbyPanel>();
    }

    public void StartOfflineBattle()
    {
        IsGameRunning = true;
        BattleManager.Instance.EnterBattle(isOnline: false, targetRoomId: 1);
    }

    /// <summary>
    /// 匹配成功：立即注册帧同步回调并加载战斗 (界面动画不能阻塞开局)
    /// </summary>
    public void StartOnlineBattle(uint roomId)
    {
        BattleManager.Instance.EnterBattle(isOnline: true, targetRoomId: roomId);
    }

    /// <summary>
    /// BattleManager 加载完战斗后调用：关闭大厅等界面，打开战斗 HUD
    /// </summary>
    public void OnBattleLoaded(BattleHudPanel.Args hud)
    {
        CloseScreens();
        UIManager.Instance.Open<BattleHudPanel, BattleHudPanel.Args>(hud);
        IsGameRunning = true;
    }

    /// <summary>
    /// 退出战斗 (返回按钮确认、联机结束)：Loading 中清理战斗，回到大厅
    /// </summary>
    public void ExitBattle()
    {
        IsGameRunning = false;
        UIManager.Instance.CloseLayer(UILayer.Hud);
        RunTransition(BattleManager.Instance.CleanupRoutine(), EnterLobby);
    }

    /// <summary>
    /// 登出：Loading 中断开网络并清理，回到登录
    /// </summary>
    public void Logout()
    {
        RunTransition(LogoutRoutine(), EnterLogin);
    }

    /// <summary>
    /// 清理会话 (网关连接、Token、地图、虚拟网络、场景视图与资源缓存)，不切换界面
    /// </summary>
    public void ResetSession()
    {
        KiHan.Network.GatewayManager.Instance?.Disconnect();
        HttpClient.Instance.ClearToken();
        MapManager.Instance.ClearMap();
        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();
        ViewManager.Instance.ClearAll();
        ResManager.Instance.Clear();
    }

    private IEnumerator LogoutRoutine()
    {
        ResetSession();
        yield return new WaitForSecondsRealtime(0.5f);
    }

    /// <summary>
    /// 显示 Loading 执行切换流程，结束后关闭 Loading 并回调
    /// </summary>
    public void RunTransition(IEnumerator routine, Action onFinish = null)
    {
        StopAllCoroutines();
        StartCoroutine(Transition(routine, onFinish));
    }

    private IEnumerator Transition(IEnumerator routine, Action onFinish)
    {
        LoadingPanel loading = UIManager.Instance.Open<LoadingPanel>();
        if (loading != null) loading.SetProgress(0f);
        yield return null;

        yield return routine;

        if (loading != null) loading.SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.2f);

        UIManager.Instance.Close<LoadingPanel>();
        onFinish?.Invoke();
    }

    /// <summary>
    /// 关闭全屏界面与弹窗 (切换流程时)
    /// </summary>
    private static void CloseScreens()
    {
        UIManager.Instance.CloseLayer(UILayer.Popup);
        UIManager.Instance.CloseLayer(UILayer.Normal);
    }
}
