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

        // 生成 P1 和 P2
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

    private PlayerView SpawnPlayer(byte gId, Vector2 pos)
    {
        Debug.Log($"[GameApp] 生成玩家: {gId} 于 {pos}");
        NarutoEntity logic = new NarutoEntity();
        logic.owner = gId;
        logic.pos = pos;
        logic.IsFacingLeft = (gId == 2);
        
        // 初始化鸣人特有资源与状态机
        logic.Init();

        // 注册到管理列表
        _players[gId] = logic;
        _allEntities.Add(logic);

        // 创建表现层 (View)
        GameObject viewGo = new GameObject($"Player_View_{gId}");
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

    private void OnStepLogic(RoomFrame frame)
    {
        if (!_isGameRunning) return;

        foreach (var kv in frame.InputFrames)
        {
            if (_players.TryGetValue(kv.Key, out var player))
            {
                player.UpdateInput(kv.Value);
            }
        }

        for (int i = 0; i < _allEntities.Count; i++)
        {
            _allEntities[i].Tick();
        }

        if (_players.TryGetValue(1, out var p1))
        {
            DoCollisionCheck(p1);
        }

        if (_players.TryGetValue(2, out var p2))
        {
            DoCollisionCheck(p2);
        }
    }

    private void DoCollisionCheck(CharacterEntity player)
    {
        foreach (var enity in _allEntities)
        {
            if (enity.owner == player.owner) continue;
            if(enity.CheckHit(player))
            {
                player.ApplyHit(enity.GetHitData());
            }
        }
    }

    #endregion
}
