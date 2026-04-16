using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;
using Managers;
using System;

public class GameApp : UnitySingleton<GameApp>
{
    [Header("Network Config")]
    public string serverIp = "127.0.0.1";
    public ushort serverPort = 9999;
    public uint roomId = 1;
    public string MapPath = "Maps/01/scen";

    private uint _myUid;
    private bool _isGameRunning = false;
    private Dictionary<byte, LogicEntity> _entities = new Dictionary<byte, LogicEntity>();

    // 开启连接网络
    private void Start()
    {
        // 生成随机UID
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在自动连接服务器...");

        NetworkManager.Instance.ip = serverIp;
        NetworkManager.Instance.port = serverPort;
        NetworkManager.Instance.Connect();

        NetworkManager.Instance.OnConnected = () => {
            byte[] req = new byte[10];
            req[0] = (byte)ClientOpCode.RoomEnterReq;
            BitConverter.GetBytes(_myUid).CopyTo(req, 1);
            BitConverter.GetBytes(roomId).CopyTo(req, 5);
            req[9] = 1; // 默认角色
            NetworkManager.Instance.Send(req);
        };
    }

    // 项目入口
    public void GameStart()
    {
        Debug.Log("[GameApp] 收到 LockstepManager 通知：服务器已开赛，开始初始化世界！");
        StartCoroutine(InitGameFlow());
    }

    private System.Collections.IEnumerator InitGameFlow()
    {
        InitCamera();
        LoadMap();

        _entities[1] = SpawnHero(1, new Vector2(-2, 1.4f));
        _entities[2] = SpawnHero(2, new Vector2(2, 1.4f));

        LockstepManager.Instance.OnLogicTick = OnLogicStep;

        _isGameRunning = true;
        Debug.Log("[GameApp] 游戏逻辑已激活，等待第一帧同步包...");
        yield return null;
    }

    private void OnLogicStep(uint frameId, Dictionary<byte, InputFrame> allInputs)
    {
        Debug.Log($"[Logic] OnLogicStep {_isGameRunning}, allinputs: {allInputs.Count}");
        if (!_isGameRunning) return;

        foreach (var kv in allInputs)
        {
            if (_entities.TryGetValue(kv.Key, out var entity))
            {
                entity.Tick(kv.Value);
            }
        }
    }

    private LogicEntity SpawnHero(byte id, Vector2 birthPos)
    {
        CharacterEntity entity = new CharacterEntity();
        entity.GameId = id;
        entity.LogicPos = birthPos;
        entity.IsFacingLeft = (id == 2);

        entity.IdleAnim = ResManager.Instance.Load<AnimationFrameData>("Characters/naruto/Idle");
        entity.RunAnim = ResManager.Instance.Load<AnimationFrameData>("Characters/naruto/Run");

        GameObject go = new GameObject($"Player_View_{id}");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100;

        var view = go.AddComponent<PlayerView>();
        view.BindEntity = entity;

        return entity;
    }

    private void InitCamera()
    {
        var cam = Camera.main;
        if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3.0f;
        cam.transform.position = new Vector3(0, 1.4f, -10);
        cam.backgroundColor = Color.black;
    }

    private void LoadMap() => ResManager.Instance.Spawn(MapPath, Vector3.zero, Quaternion.identity);
}