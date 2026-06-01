using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;
using System;
using System.Collections;
using Managers;
using KiHan.View.UI.Login;

public class GameApp : UnitySingleton<GameApp>
{
    [Header("Network Config")]
    public uint roomId = 1;
    public string MapPath = "Maps/01/scen";

    [Header("Prefabs")]
    public GameObject playerViewPrefab; 

    private uint _myUid;
    private byte _myGameId = 1;
    private bool _isGameRunning = false;
    public bool IsGameRunning => _isGameRunning;

    private View.UI.BattleUIPanel _combatUI;
    private GameObject _combatUIGo;

    // --- 性能监控变量 ---
    private float _logicFpsTimer;
    private int _logicFrameCount;

    private void Start()
    {
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在初始化网络...");

        // 启动 UI 框架与登录界面
        UIManager.Instance.OpenPanel<LoginPanel>(UIConst.LoginPanel);
    }

    public void StartOfflineGame()
    {
        Debug.Log("[GameApp] 开始进入单机模式...");
        // 1. 初始化虚拟网络实现
        var net = VirtualNetworkManager.Instance;

        // 2. 初始化同步层并注入网络
        LockstepManager.Instance.Init(net);
        LockstepManager.Instance.OnExecuteFrame = OnStepLogic;

        // 3. 监听协议
        net.OnOpCodeReceived -= HandleNetworkMessage; // 防止重复注册
        net.OnOpCodeReceived += HandleNetworkMessage;
        
        // 触发连接，VirtualNetworkManager 会立刻返回 RoomEnterResp 和 GameStartNtf
        net.Connect();
    }

    private void HandleNetworkMessage(ServerOpCode op, ArraySegment<byte> payload)
    {
        switch (op)
        {
            case ServerOpCode.RoomEnterResp:
                _myGameId = payload.Array[payload.Offset + 5];
                Debug.Log($"[GameApp] 分配 GameId: {_myGameId}");
                break;
            case ServerOpCode.GameStartNtf:
                GameStart();
                break;
        }
    }

    public void GameStart()
    {
        if (_isGameRunning) return;
        
        StartCoroutine(LoadGameAsync());
    }

    private IEnumerator LoadGameAsync()
    {
        // 1. 加载并显示 Loading 界面
        GameObject loadingPrefab = Resources.Load<GameObject>("UI/Loading/Canvas");
        GameObject loadingGo = null;
        UnityEngine.UI.Slider slider = null;
        
        if (loadingPrefab != null)
        {
            loadingGo = Instantiate(loadingPrefab);
            loadingGo.name = "LoadingUI";
            slider = loadingGo.GetComponentInChildren<UnityEngine.UI.Slider>(true);
            if (slider != null) slider.value = 0;
            
            // 确保LoadingUI在最上层
            Canvas canvas = loadingGo.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = 999;
        }

        // 模拟一点进度
        if (slider != null) slider.value = 0.2f;
        yield return null;

        // 2. 异步加载地图
        Debug.Log($"[GameApp] 开始初始化世界，异步加载地图: {MapPath}");
        var mapReq = ResManager.Instance.LoadAsync<GameObject>(MapPath);
        while (!mapReq.isDone)
        {
            if (slider != null) slider.value = 0.2f + mapReq.progress * 0.4f;
            yield return null;
        }
        ResManager.Instance.AddToCache(MapPath, mapReq.asset);
        MapManager.Instance.LoadMap(MapPath);

        if (slider != null) slider.value = 0.6f;

        // 3. 异步加载战斗UI
        string uiPath = "UI/Button/Canvas";
        var uiReq = ResManager.Instance.LoadAsync<GameObject>(uiPath);
        while (!uiReq.isDone)
        {
            if (slider != null) slider.value = 0.6f + uiReq.progress * 0.4f;
            yield return null;
        }
        ResManager.Instance.AddToCache(uiPath, uiReq.asset);

        // 4. 初始化场景逻辑
        SceneManager.Instance.InitWorld();
        InitCombatUI(uiReq.asset as GameObject);

        // --- 5. 生成玩家 ---
        PlayerView p1View = SceneManager.Instance.SpawnPlayer(1, new Vector2(-2, 0), playerViewPrefab, _combatUI, _myGameId);
        PlayerView p2View = SceneManager.Instance.SpawnPlayer(2, new Vector2(2, 0), playerViewPrefab, _combatUI, _myGameId);

        // 相机追踪：直接追踪本地玩家的逻辑实体
        CharacterEntity targetPlayer = SceneManager.Instance.GetPlayer(_myGameId);
        if (targetPlayer != null)
        {
            Debug.Log($"[GameApp] 相机追踪目标设为 Player_{_myGameId} (逻辑对象)");
            CameraControllor.Instance.SetTarget(targetPlayer, true);
        }
        else
        {
            Debug.LogError("[GameApp] 未能找到相机追踪的逻辑目标！");
        }

        if (slider != null) slider.value = 1.0f;
        yield return new WaitForSeconds(0.2f); // 稍微停顿一下展示满进度

        // 清理Loading
        if (loadingGo != null)
        {
            Destroy(loadingGo);
        }

        // 隐藏大厅
        UIManager.Instance.ClosePanel(UIConst.LobbyPanel);

        _isGameRunning = true;
        Debug.Log("[GameApp] 战斗开始！");
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

            // 绑定退出按钮
            UnityEngine.UI.Button[] btns = _combatUIGo.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var btn in btns)
            {
                if (btn.gameObject.name.ToLower().Contains("back"))
                {
                    btn.onClick.AddListener(ExitGame);
                    break;
                }
            }

            Debug.Log("[GameApp] 战斗 UI 初始化并绑定成功。");
        }
        else
        {
            Debug.LogError($"[GameApp] 错误：战斗 UI 预制体为空");
        }
    }

    public void ExitGame()
    {
        Debug.Log("[GameApp] 退出战斗");
        _isGameRunning = false;
        
        // 销毁场景实体
        SceneManager.Instance.InitWorld();
        
        // 销毁地图
        MapManager.Instance.ClearMap();
        
        // 停止单机网络循环
        if (VirtualNetworkManager.Instance != null)
        {
            VirtualNetworkManager.Instance.Stop();
        }

        // 销毁战斗UI
        if (_combatUIGo != null)
        {
            Destroy(_combatUIGo);
            _combatUIGo = null;
        }

        // 重置相机
        CameraControllor.Instance.SetTarget(null);
        CameraControllor.Instance.transform.position = new Vector3(0, CameraControllor.Instance.yOffset, -10f);

        // 清除表现层映射和残留对象
        Managers.ViewManager.Instance.ClearAll();
        Managers.ResManager.Instance.Clear();

        // 回到大厅
        UIManager.Instance.OpenPanel<KiHan.View.UI.Lobby.LobbyPanel>(UIConst.LobbyPanel);
    }

    #region

    private void FPSCounter()
    {
        _logicFrameCount++;
        float now = Time.realtimeSinceStartup;
        if (now - _logicFpsTimer >= 1.0f)
        {
            float actualFps = _logicFrameCount / (now - _logicFpsTimer);
            // 预期是 15 FPS 左右
            Debug.Log($"<color=cyan>[Monitor] 逻辑帧率: {actualFps:F1}");
            _logicFrameCount = 0;
            _logicFpsTimer = now;
        }
    }

    private void OnStepLogic(RoomFrame frame)
    {
        FPSCounter();
        
        if (!_isGameRunning) return;

        // 1. 同步输入
        SceneManager.Instance.ApplyInputs(frame.InputFrames);

        // 2. 逻辑和碰撞交给统一的管理器
        SceneManager.Instance.TickAll();
    }

    #endregion
}
