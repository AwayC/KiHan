using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;
using System;
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

    private View.UI.BattleUIPanel _combatUI;

    // --- 性能监控变量 ---
    private float _logicFpsTimer;
    private int _logicFrameCount;

    private void Start()
    {
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在初始化网络...");

        // 启动 UI 框架与登录界面
        //UIManager.Instance.OpenPanel<LoginPanel>(UIConst.LoginPanel);

        // 1. 初始化虚拟网络实现
        var net = VirtualNetworkManager.Instance;

        // 2. 初始化同步层并注入网络
        LockstepManager.Instance.Init(net);
        LockstepManager.Instance.OnExecuteFrame = OnStepLogic;

        // 3. 监听协议
        net.OnOpCodeReceived += HandleNetworkMessage;
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
        
        // 初始化场景
        InitWorld();

        _isGameRunning = true;
        Debug.Log("[GameApp] 战斗开始！");
    }

    private void InitWorld()
    {
        Debug.Log($"[GameApp] 开始初始化世界，地图: {MapPath}");
        MapManager.Instance.LoadMap(MapPath);

        SceneManager.Instance.InitWorld();

        // --- 1. 优先初始化战斗 UI ---
        InitCombatUI();

        // --- 2. 然后生成玩家 ---
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
    }

    private void InitCombatUI()
    {
        string path = "UI/Button/Canvas";
        Debug.Log($"[GameApp] 尝试加载战斗 UI: {path}");
        
        GameObject uiPrefab = ResManager.Instance.Load<GameObject>(path);
        if (uiPrefab != null)
        {
            GameObject uiGo = Instantiate(uiPrefab);
            uiGo.name = "Battle_UI";

            // 核心修复：如果预制体上没挂脚本，代码自动挂载
            _combatUI = uiGo.GetComponent<View.UI.BattleUIPanel>();
            if (_combatUI == null)
            {
                Debug.Log("[GameApp] 检测到预制体缺少 BattleUIPanel 脚本，正在自动注入...");
                _combatUI = uiGo.AddComponent<View.UI.BattleUIPanel>();
            }
            Debug.Log("[GameApp] 战斗 UI 初始化并绑定成功。");
        }
        else
        {
            Debug.LogError($"[GameApp] 错误：无法从 Resources 加载预制体: {path}");
        }
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
