using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;
using System;
using Managers;

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

    // 严格按照架构设计的容器
    private Dictionary<byte, CharacterEntity> _players = new Dictionary<byte, CharacterEntity>();
    private List<LogicEntity> _allEntities = new List<LogicEntity>();

    private void Start()
    {
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在初始化网络...");

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

        // --- 1. 优先初始化战斗 UI ---
        InitCombatUI();

        // --- 2. 然后生成玩家 (这样 SpawnPlayer 里的 SetupIcons 才能找到 _combatUI) ---
        PlayerView p1View = SpawnPlayer(1, new Vector2(-2, 0));
        PlayerView p2View = SpawnPlayer(2, new Vector2(2, 0));

        // 相机追踪：直接追踪本地玩家的逻辑实体
        CharacterEntity targetPlayer;
        if (_players.TryGetValue(_myGameId, out targetPlayer))
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

    private int _nextEntityId = 1;

    private PlayerView SpawnPlayer(byte gId, Vector2 pos)
    {
        Debug.Log($"[GameApp] 生成玩家: {gId} 于 {pos}");
        NarutoEntity logic = new NarutoEntity();
        logic.EntityId = _nextEntityId++;
        logic.owner = gId;
        logic.pos = pos;
        logic.IsFacingLeft = (gId == 2);
        
        // 初始化鸣人特有资源与状态机
        logic.Init();

        // --- 核心：当本地角色“加载”时，同步 UI 按键图标 ---
        if (gId == _myGameId && _combatUI != null)
        {
            _combatUI.SetupIcons(logic.CharacterId);
        }

        // 注册到管理列表
        _players[gId] = logic;
        _allEntities.Add(logic);

        // 创建表现层 (View)
        GameObject viewGo = new GameObject($"Player_View_{gId}");
        viewGo.transform.position = new Vector3(logic.pos.x, logic.pos.y, 0); // 瞬间同步初始位置
        
        var view = viewGo.AddComponent<PlayerView>();
        view.BindEntity = logic;
        
        // 如果有 Prefab 则实例化模型层
        if (playerViewPrefab != null)
        {
            Instantiate(playerViewPrefab, viewGo.transform);
        }

        return view;
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
        foreach (var kv in frame.InputFrames)
        {
            if (_players.TryGetValue(kv.Key, out var player))
            {
                player.UpdateInput(kv.Value);
            }
        }

        // 2. 逻辑 Tick (状态机、物理位移)
        // 注意：此时不推进动画帧索引，确保碰撞检测看到的是当前显示的帧
        for (int i = 0; i < _allEntities.Count; i++)
        {
            _allEntities[i].Tick();
        }

        // 3. 碰撞检测
        if (_players.TryGetValue(1, out var p1)) DoCollisionCheck(p1);
        if (_players.TryGetValue(2, out var p2)) DoCollisionCheck(p2);

        // 4. 推进动画帧 (Post Tick)
        for (int i = 0; i < _allEntities.Count; i++)
        {
            _allEntities[i].AdvanceAnimation();
        }
    }

    private void DoCollisionCheck(CharacterEntity target)
    {
        foreach (var attacker in _allEntities)
        {
            if (attacker.owner == target.owner) continue;
            
            // 判定：攻击者是否有攻击盒，且目标是否有受击盒，且未被此动作命中过
            if(attacker.CheckHit(target))
            {
                if (attacker.CanHit(target))
                {
                    target.ApplyHit(attacker.GetHitData());
                    attacker.RegisterHit(target); // 标记命中，防止同一段动作重复打击

                    // 触发相机打击感反馈
                    CameraControllor.Instance.ImpactEffect();
                }
            }
        }
    }

    #endregion
}
