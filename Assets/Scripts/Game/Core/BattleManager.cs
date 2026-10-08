using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KiHan.Logic;
using KiHan.Network;
using Managers;

public interface IBattleFactory
{
    void CreateBattle(uint roomId);
}

public class OfflineBattleFactory : IBattleFactory
{
    public void CreateBattle(uint roomId)
    {
        Debug.Log("[OfflineBattleFactory] 创建单机战斗...");
        LockstepManager.Instance.Init(VirtualNetworkManager.Instance);
        VirtualNetworkManager.Instance.Connect();
    }
}

public class OnlineBattleFactory : IBattleFactory
{
    public void CreateBattle(uint roomId)
    {
        Debug.Log("[OnlineBattleFactory] 创建联机战斗...");
        var gateway = GatewayManager.Instance;
        if (gateway != null && gateway.IsAuthed)
        {
            KcpNetworkManager.Instance.Connect(gateway.ServerIp, (ushort)gateway.UdpPort, gateway.ConnId, gateway.ConnKey);
            LockstepManager.Instance.Init(KcpNetworkManager.Instance);
            var req = new EnterRoomReq { room_id = (int)roomId };
            KcpNetworkManager.Instance.SendMsg(2001, req.Serialize());
        }
        else
        {
            Debug.LogError("[OnlineBattleFactory] 联机模式启动失败：尚未登录网关");
        }
    }
}

/// <summary>
/// 战斗生命周期与网络帧驱动中心
/// </summary>
public class BattleManager : UnitySingleton<BattleManager>
{
    public uint roomId = 1;
    public bool IsOnline = false;

    public void EnterBattle(bool isOnline, uint targetRoomId = 1)
    {
        this.roomId = targetRoomId;
        this.IsOnline = isOnline;

        // 连接网络层
        IBattleFactory factory = isOnline ? new OnlineBattleFactory() : (IBattleFactory)new OfflineBattleFactory();
        factory.CreateBattle(roomId);

        // 绑定帧同步回调
        LockstepManager.Instance.OnExecuteFrame = OnBattleTick;
        LockstepManager.Instance.OnGameStart -= OnGameStart;
        LockstepManager.Instance.OnGameStart += OnGameStart;
    }

    public void OnGameStart()
    {
        Debug.Log("[BattleManager] 收到游戏开始信号，准备进入战斗流程...");
    }

    public void OnBattleTick(RoomFrame frame)
    {
        if (!GameApp.Instance.IsGameRunning) return;
        // 等待新架构战斗循环接入
    }

    public void ExitBattle(Action onComplete)
    {
        GameApp.Instance.PerformTransitionAsync(ExitBattleRoutine(onComplete));
    }

    private IEnumerator ExitBattleRoutine(Action onFinished)
    {
        Debug.Log("[BattleManager] 正在清理战斗资源...");
        GameApp.Instance.SetGameRunning(false);
        LockstepManager.Instance.OnExecuteFrame = null;
        LockstepManager.Instance.StopGame();

        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();
        if (KcpNetworkManager.Instance != null && KcpNetworkManager.Instance.IsConnected)
        {
            KcpNetworkManager.Instance.Disconnect();
        }

        SceneManager.Instance.InitWorld();
        MapManager.Instance.ClearMap();

        Managers.ViewManager.Instance.ClearAll();
        Managers.ResManager.Instance.Clear();

        CameraControllor.Instance.SetTarget(null);

        yield return new WaitForSecondsRealtime(0.3f);
        onFinished?.Invoke();
    }
}
