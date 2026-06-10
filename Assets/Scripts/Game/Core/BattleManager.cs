using System;
using System.Collections;
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
        BattleManager.Instance.ActiveRoom = new LSRoom(roomId);
        LockstepManager.Instance.Init(VirtualNetworkManager.Instance);
        VirtualNetworkManager.Instance.Connect();
    }
}

public class OnlineBattleFactory : IBattleFactory
{
    public void CreateBattle(uint roomId)
    {
        Debug.Log("[OnlineBattleFactory] 创建联机战斗...");
        BattleManager.Instance.ActiveRoom = new LSRoom(roomId);

        var gateway = GatewayManager.Instance;
        if (gateway != null && gateway.IsAuthed)
        {
            // 建立 KCP 通道
            KcpNetworkManager.Instance.Connect(gateway.ServerIp, (ushort)gateway.UdpPort, gateway.ConnId, gateway.ConnKey);
            
            // 注册消息监听到 KCP 网络通道
            LockstepManager.Instance.Init(KcpNetworkManager.Instance);
            
            // 发送 EnterRoomReq (由于这是 >= 2000 的协议，它现在应该通过 KCP 通道发送)
            Debug.Log($"[OnlineBattleFactory] Sending EnterRoomReq (2001) for room {roomId}");
            var req = new EnterRoomReq { room_id = (int)roomId };
            KcpNetworkManager.Instance.SendMsg(2001, req.Serialize());
        }
        else
        {
            Debug.LogError("[OnlineBattleFactory] 联机模式启动失败：尚未登录网关");
        }
    }
}

public class BattleManager : UnitySingleton<BattleManager>
{
    public uint roomId = 1;
    public string mapPath = "Maps/01/scen";
    public GameObject playerViewPrefab; 

    private View.UI.BattleUIPanel _combatUI;
    private GameObject _combatUIGo;
    
    // 性能监控
    private float _logicFpsTimer;
    private int _logicFrameCount;

    public LSRoom ActiveRoom { get; set; }

    /// <summary>
    /// 进入战斗入口
    /// </summary>
    public void EnterBattle(bool isOnline, uint targetRoomId = 1)
    {
        this.roomId = targetRoomId;
        
        // 1. 明确网络职责：通知网络层连接（单机就连虚拟网，联机就连真实网）
        IBattleFactory factory = isOnline ? new OnlineBattleFactory() : (IBattleFactory)new OfflineBattleFactory();
        factory.CreateBattle(roomId);

        // 2. 明确同步层职责：解耦回调，让 LockstepManager 直接把帧喷给 BattleManager
        LockstepManager.Instance.OnExecuteFrame = OnBattleTick;
        LockstepManager.Instance.OnGameStart -= OnGameStart;
        LockstepManager.Instance.OnGameStart += OnGameStart;
    }

    public void OnGameStart()
    {
        // 3. 收到服务器的开始指令，执行加载流程
        GameApp.Instance.PerformTransitionAsync(LoadBattleRoutine());
    }

    /// <summary>
    /// 帧同步的核心驱动：由 LockstepManager 收集完完整帧后，统一调用这里
    /// </summary>
    private void OnBattleTick(RoomFrame frame)
    {
        if (!GameApp.Instance.IsGameRunning) return;
        
        // 性能统计归我管
        FPSCounter();

        // 驱动逻辑层沙盒：把帧输入交给场景实体
        ActiveRoom?.Tick(frame);
    }

    private void FPSCounter()
    {
        _logicFrameCount++;
        float now = Time.realtimeSinceStartup;
        if (now - _logicFpsTimer >= 1.0f)
        {
            float actualFps = _logicFrameCount / (now - _logicFpsTimer);
            Debug.Log($"<color=cyan>[Monitor] 逻辑帧率: {actualFps:F1}</color>");
            _logicFrameCount = 0;
            _logicFpsTimer = now;
        }
    }

    public void ExitBattle(Action onComplete)
    {
        GameApp.Instance.PerformTransitionAsync(ExitBattleRoutine(onComplete));
    }

    private IEnumerator LoadBattleRoutine()
    {
        yield return null;

        // 1. 异步加载地图
        var mapReq = ResManager.Instance.LoadAsync<GameObject>(mapPath);
        while (!mapReq.isDone) yield return null;
        ResManager.Instance.AddToCache(mapPath, mapReq.asset);
        MapManager.Instance.LoadMap(mapPath);

        // 2. 异步加载战斗UI
        string uiPath = "UI/Button/Canvas";
        var uiReq = ResManager.Instance.LoadAsync<GameObject>(uiPath);
        while (!uiReq.isDone) yield return null;
        ResManager.Instance.AddToCache(uiPath, uiReq.asset);

        // 3. 初始化逻辑与视图容器
        SceneManager.Instance.InitWorld();
        InitCombatUI(uiReq.asset as GameObject);

        // 4. 生成玩家 (从 LockstepManager 获取 MyGameId, 但 Virtual 默认为 1)
        byte myGameId = LockstepManager.Instance.MyGameId;
        Debug.Log($"[BattleManger] my game id {myGameId}");
        if (myGameId == 0) myGameId = 1; // 单机保底

        // 加载并绑定 playerViewPrefab。这里需要确保您在 Inspector 里给 BattleManager 挂载了 Prefab。
        // 如果预制体为空，尝试使用 Resources 动态加载
        if (playerViewPrefab == null)
        {
            var pReq = Resources.LoadAsync<GameObject>("Characters/naruto/naruto");
            while (!pReq.isDone) yield return null;
            playerViewPrefab = pReq.asset as GameObject;
        }

        SceneManager.Instance.SpawnPlayer(1, new Vector2(-2, 0), playerViewPrefab, _combatUI, myGameId);
        SceneManager.Instance.SpawnPlayer(2, new Vector2(2, 0), playerViewPrefab, _combatUI, myGameId);

        CharacterEntity targetPlayer = ActiveRoom?.GetPlayer(myGameId);
        if (targetPlayer != null) CameraControllor.Instance.SetTarget(targetPlayer, true);

        UIManager.Instance.ClosePanel(UIConst.LobbyPanel);
        GameApp.Instance.SetGameRunning(true);
    }

    private void InitCombatUI(GameObject uiPrefab)
    {
        if (uiPrefab != null)
        {
            _combatUIGo = Instantiate(uiPrefab);
            _combatUIGo.name = "Battle_UI";

            _combatUI = _combatUIGo.GetComponent<View.UI.BattleUIPanel>();
            if (_combatUI == null)
            {
                _combatUI = _combatUIGo.AddComponent<View.UI.BattleUIPanel>();
            }
        }
    }

    private IEnumerator ExitBattleRoutine(Action onFinished)
    {
        Debug.Log("[BattleManager] 正在清理战斗资源...");
        GameApp.Instance.SetGameRunning(false);
        
        ActiveRoom = null;
        LockstepManager.Instance.OnExecuteFrame = null;
        LockstepManager.Instance.StopGame();

        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();

        // 销毁实体与地图视图
        SceneManager.Instance.InitWorld();
        MapManager.Instance.ClearMap();
        
        // 销毁 UI
        if (_combatUIGo != null)
        {
            Destroy(_combatUIGo);
            _combatUIGo = null;
        }

        // 清理缓存
        Managers.ViewManager.Instance.ClearAll();
        Managers.ResManager.Instance.Clear();

        // 重置相机
        CameraControllor.Instance.SetTarget(null);
        CameraControllor.Instance.transform.position = new Vector3(0, CameraControllor.Instance.yOffset, -10f);

        yield return new WaitForSecondsRealtime(0.3f);
        onFinished?.Invoke();
    }
}