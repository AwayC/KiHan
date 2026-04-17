using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;
using System;

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
    // 快照缓存：FrameId -> (GameId -> ActorState)
    private Dictionary<uint, Dictionary<byte, GameActor.ActorState>> _worldSnapshots = new Dictionary<uint, Dictionary<byte, GameActor.ActorState>>();

    private void Start()
    {
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在自动连接...");

        if (NetworkManager.Instance != null)
        {
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
        
        InitWorld();
        
        LockstepManager.Instance.OnExecuteFrame = OnStepLogic;
        LockstepManager.Instance.OnSaveSnapshot = OnSaveWorldSnapshot;
        LockstepManager.Instance.OnLoadSnapshot = OnLoadWorldSnapshot;

        _isGameRunning = true;
    }

    private void InitWorld()
    {
        // 1. 地图初始化
        MapManager.Instance.LoadMap(MapPath);

        // 2. 玩家初始化
        _actors[1] = SpawnPlayer(1, new Vector2(-2, 1.4f));
        _actors[2] = SpawnPlayer(2, new Vector2(2, 1.4f));

        // 3. 相机追踪自己
        if (_actors.TryGetValue(_myGameId, out var myActor))
        {
            CameraControllor.Instance.SetTarget(myActor.transform);
        }
        else if (_actors.Count > 0)
        {
            // 兜底：如果没找到自己，追踪 ID 1
            CameraControllor.Instance.SetTarget(_actors[1].transform);
        }
    }

    private GameActor SpawnPlayer(byte gId, Vector2 pos)
    {
        GameObject actorGo = new GameObject($"Actor_{gId}");
        GameActor actor = actorGo.AddComponent<GameActor>();

        CharacterEntity logic = new CharacterEntity();
        logic.GameId = gId;
        logic.LogicPos = pos;
        logic.IsFacingLeft = (gId == 2);
        
        var idleData = ResManager.Instance.Load<AnimationFrameData>("Characters/naruto/Idle");
        logic.AddState(new DefaultIdleState { IdleAnim = idleData });
        logic.ChangeState(0); 

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

    private void OnStepLogic(GameFrame frame)
    {
        if (!_isGameRunning) return;

        for (int i = 0; i < frame.AllPlayerInputs.Length; i++)
        {
            byte actorId = (byte)(i + 1); 
            if (_actors.TryGetValue(actorId, out var actor))
            {
                actor.LogicTick(frame.AllPlayerInputs[i]);
            }
        }
    }

    private void OnSaveWorldSnapshot(uint frameId)
    {
        var snapshot = new Dictionary<byte, GameActor.ActorState>();
        foreach (var kv in _actors)
        {
            snapshot[kv.Key] = kv.Value.SaveState();
        }
        _worldSnapshots[frameId] = snapshot;

        if (frameId > 100) _worldSnapshots.Remove(frameId - 100);
    }

    private void OnLoadWorldSnapshot(uint frameId)
    {
        if (_worldSnapshots.TryGetValue(frameId, out var snapshot))
        {
            foreach (var kv in snapshot)
            {
                if (_actors.TryGetValue(kv.Key, out var actor))
                {
                    actor.LoadState(kv.Value);
                }
            }
        }
    }

    #endregion
}
