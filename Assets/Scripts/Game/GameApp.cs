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

        // 启动 UI 框架与登录界面 (调试后门已禁用正常启动)
        // UIManager.Instance.OpenPanel<LoginPanel>(UIConst.LoginPanel);

        // --- 调试后门：启动即进入单机战斗 ---
        StartOfflineGame();
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
        PerformTransitionAsync(LoadGameRoutine());
    }

    /// <summary>
    /// 通用异步场景切换接口
    /// </summary>
    public void PerformTransitionAsync(IEnumerator transitionRoutine, Action onFinish = null)
    {
        StopAllCoroutines();
        StartCoroutine(UnifiedTransition(transitionRoutine, onFinish));
    }

    private IEnumerator UnifiedTransition(IEnumerator transitionRoutine, Action onFinish)
    {
        // 1. 显示 Loading 界面
        GameObject loadingPrefab = Resources.Load<GameObject>("UI/Loading/Canvas");
        GameObject loadingGo = null;
        UnityEngine.UI.Slider slider = null;
        TMPro.TMP_Text percentText = null;
        
        if (loadingPrefab != null)
        {
            loadingGo = Instantiate(loadingPrefab);
            loadingGo.name = "LoadingUI";
            slider = loadingGo.GetComponentInChildren<UnityEngine.UI.Slider>(true);
            
            TMPro.TMP_Text[] texts = loadingGo.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name.ToLower().Contains("num") || t.gameObject.name.ToLower().Contains("text"))
                {
                    percentText = t;
                    break;
                }
            }
            
            Canvas canvas = loadingGo.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = 999;
        }

        // 同步更新进度显示
        Action<float> updateUI = (p) => {
            if (slider != null) slider.value = p;
            if (percentText != null) percentText.text = (p * 100f).ToString("F2") + "%";
        };

        updateUI(0f);
        yield return null;

        // 2. 执行具体的加载/卸载任务
        // 传入 updateUI 供具体的 Routine 调用（如果需要的话，或者 Routine 自己控制进度）
        // 这里为了简单，我们让具体的 Routine 运行，如果它需要汇报进度，可以通过静态变量或闭包，
        // 也可以通过 YieldReturn 一个 Float 值。
        // 这里我们约定 Routine 内部自己管理大段的进度。
        
        yield return transitionRoutine;

        // 3. 完成
        updateUI(1.0f);
        yield return new WaitForSecondsRealtime(0.2f);

        if (loadingGo != null) Destroy(loadingGo);
        onFinish?.Invoke();
    }

    private IEnumerator LoadGameRoutine()
    {
        // 这里的进度汇报逻辑需要和内部的 mapReq/uiReq 结合
        // 由于 PerformTransitionAsync 已经接管了 LoadingUI 的生命周期，这里只需要 yield 具体任务

        // 暂时先复用之前的分段进度逻辑，之后可以封装得更优雅
        
        // 模拟一点初始进度
        yield return null;

        // 1. 异步加载地图
        var mapReq = ResManager.Instance.LoadAsync<GameObject>(MapPath);
        while (!mapReq.isDone) yield return null;
        ResManager.Instance.AddToCache(MapPath, mapReq.asset);
        MapManager.Instance.LoadMap(MapPath);

        // 2. 异步加载战斗UI
        string uiPath = "UI/Button/Canvas";
        var uiReq = ResManager.Instance.LoadAsync<GameObject>(uiPath);
        while (!uiReq.isDone) yield return null;
        ResManager.Instance.AddToCache(uiPath, uiReq.asset);

        // 3. 初始化逻辑
        SceneManager.Instance.InitWorld();
        InitCombatUI(uiReq.asset as GameObject);

        // 4. 生成玩家
        SceneManager.Instance.SpawnPlayer(1, new Vector2(-2, 0), playerViewPrefab, _combatUI, _myGameId);
        SceneManager.Instance.SpawnPlayer(2, new Vector2(2, 0), playerViewPrefab, _combatUI, _myGameId);

        CharacterEntity targetPlayer = SceneManager.Instance.GetPlayer(_myGameId);
        if (targetPlayer != null) CameraControllor.Instance.SetTarget(targetPlayer, true);

        UIManager.Instance.ClosePanel(UIConst.LobbyPanel);
        _isGameRunning = true;
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

            Debug.Log("[GameApp] 战斗 UI 初始化并绑定成功。");
        }
        else
        {
            Debug.LogError($"[GameApp] 错误：战斗 UI 预制体为空");
        }
    }

    private IEnumerator ExitGameRoutine()
    {
        Debug.Log("[GameApp] 正在清理战斗资源...");
        _isGameRunning = false;
        
        // 停止网络
        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();

        // 销毁实体与地图
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

        yield return new WaitForSecondsRealtime(0.3f); // 模拟一点卸载时间

        // 回到大厅
        UIManager.Instance.OpenPanel<KiHan.View.UI.Lobby.LobbyPanel>(UIConst.LobbyPanel);
    }

    public void ExitGame()
    {
        PerformTransitionAsync(ExitGameRoutine());
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
