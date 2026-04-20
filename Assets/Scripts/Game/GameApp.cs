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
    private byte _myGameId;
    private bool _isGameRunning = false;

    // 管理所有实体的容器
    private Dictionary<byte, GameActor> _actors = new Dictionary<byte, GameActor>();

    private void Start()
    {
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在初始化虚拟网络...");

        // 强制确保 VirtualNetworkManager 存在并作为 NetworkManager 的实现
        // 这样后续调用 NetworkManager.Instance 就会拿到这个虚拟实现
        var net = VirtualNetworkManager.Instance; 

        if (NetworkManager.Instance != null)
        {
            // 注入网络层实现
            LockstepManager.Instance.Init(NetworkManager.Instance);

            NetworkManager.Instance.OnOpCodeReceived += HandleNetworkMessage;
            NetworkManager.Instance.Connect();
        }
    }

    private void HandleNetworkMessage(ServerOpCode op, ArraySegment<byte> payload)
    {
        switch (op)
        {
            case ServerOpCode.RoomEnterResp:
                if (payload.Count >= 6)
                {
                    _myGameId = payload.Array[payload.Offset + 5];
                    Debug.Log($"[GameApp] 进房成功，分配 GameId: {_myGameId}");
                }
                break;
            case ServerOpCode.GameStartNtf:
                GameStart();
                break;
        }
    }

    public void GameStart()
    {
        if (_isGameRunning) return;
        Debug.Log("[GameApp] 战斗开始通知，初始化战场...");
        
        // 1. 初始化世界（地图和玩家）
        InitWorld();
        
        // 2. 绑定严格帧同步回调
        LockstepManager.Instance.OnExecuteFrame = OnStepLogic;

        _isGameRunning = true;
    }

    private void InitWorld()
    {
        // 1. 地图初始化
        MapManager.Instance.LoadMap(MapPath);

        // 2. 玩家初始化 (此处为原型演示，固定生成 1 和 2 号)
        _actors[1] = SpawnPlayer(1, new Vector2(-2, 1.4f));
        _actors[2] = SpawnPlayer(2, new Vector2(2, 1.4f));

        // 3. 相机追踪自己
        if (_actors.TryGetValue(_myGameId, out var myActor))
        {
            CameraControllor.Instance.SetTarget(myActor.transform);
        }
        else if (_actors.Count > 0)
        {
            CameraControllor.Instance.SetTarget(_actors[1].transform);
        }
    }

    private GameActor SpawnPlayer(byte gId, Vector2 pos)
    {
        GameObject actorGo = new GameObject($"Actor_{gId}");
        GameActor actor = actorGo.AddComponent<GameActor>();

        NarutoEntity logic = new NarutoEntity();
        logic.GameId = gId;
        logic.LogicPos = pos;
        logic.IsFacingLeft = (gId == 2);
        
        // 调用鸣人特有的初始化 (加载所有状态和动画)
        logic.Init();

        if (playerViewPrefab != null)
        {
            actor.Init(logic, playerViewPrefab);
        }
        else
        {
            GameObject viewGo = new GameObject("View");
            viewGo.transform.SetParent(actorGo.transform);
            var view = viewGo.AddComponent<PlayerView>();
            view.BindEntity = logic;
        }

        return actor;
    }

    #region 同步层回调

    private void OnStepLogic(RoomFrame frame)
    {
        if (!_isGameRunning) return;

        // 1. 驱动所有实体执行输入逻辑
        foreach (var kv in frame.InputFrames)
        {
            if (_actors.TryGetValue(kv.Key, out var actor))
            {
                actor.LogicTick(kv.Value);
            }
        }

        // 2. 全局碰撞检测 (这就是如何通知对方受击的地方)
        // 遍历所有可能的攻击者和受击者
        foreach (var attackerKv in _actors)
        {
            foreach (var victimKv in _actors)
            {
                if (attackerKv.Key == victimKv.Key) continue; // 不能自己打自己

                var attacker = attackerKv.Value.Logic;
                var victim = victimKv.Value.Logic;

                // 检查攻击判定
                if (attacker.CheckHit(victim))
                {
                    // 如果命中了，直接修改受击者的逻辑状态
                    victim.TakeDamage(1); 
                    Debug.Log($"[Battle] Player {attackerKv.Key} hit Player {victimKv.Key}!");
                }
            }
        }
    }

    #endregion
}
