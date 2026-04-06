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

    // --- 1. 项目启动入口 ---
    private void Start()
    {
        // 自动生成 UID
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动成功！我的 UID: {_myUid}。正在自动连接服务器...");

        // 自动连接
        NetworkManager.Instance.ip = serverIp;
        NetworkManager.Instance.port = serverPort;
        NetworkManager.Instance.Connect();

        // 连上后立即请求入场
        NetworkManager.Instance.OnConnected = () => {
            byte[] req = new byte[10];
            req[0] = (byte)ClientOpCode.RoomEnterReq;
            BitConverter.GetBytes(_myUid).CopyTo(req, 1);
            BitConverter.GetBytes(roomId).CopyTo(req, 5);
            req[9] = 1; // 默认角色
            NetworkManager.Instance.Send(req);
        };
    }

    // --- 2. 核心：LockstepManager 调用的开赛入口 ---
    public void GameStart()
    {
        Debug.Log("[GameApp] 收到 LockstepManager 通知：服务器已开赛，开始初始化世界！");
        StartCoroutine(InitGameFlow());
    }

    private System.Collections.IEnumerator InitGameFlow()
    {
        // A. 环境初始化
        InitCamera();
        LoadMap();

        // B. 实体生成
        // 约定：1v1 中，gameId 为 1 的在左，2 的在右
        _entities[1] = SpawnHero(1, new Vector2(-2, 1.4f));
        _entities[2] = SpawnHero(2, new Vector2(2, 1.4f));

        // C. 绑定逻辑步进循环
        LockstepManager.Instance.OnLogicTick = OnLogicStep;

        _isGameRunning = true;
        Debug.Log("[GameApp] 游戏逻辑已激活，等待第一帧同步包...");
        yield return null;
    }

    // --- 3. 逻辑驱动：每一帧同步包到达时执行 ---
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

    // --- 内部辅助生成方法 ---
    private LogicEntity SpawnHero(byte id, Vector2 birthPos)
    {
        LogicEntity entity = new LogicEntity();
        entity.LogicPos = birthPos;
        entity.IsFacingLeft = (id == 2);

        // 1. 同步加载动画配置
        entity.IdleAnim = ResManager.Instance.Load<AnimationFrameData>("Characters/naruto/Idle");
        entity.RunAnim = ResManager.Instance.Load<AnimationFrameData>("Characters/naruto/Run");

        // 【关键修改 1】：强制初始化当前动画，防止第一帧没图导致 View 层显示 Null
        //entity.SwitchAnimation(entity.IdleAnim);

        // 2. 生成视觉 View
        GameObject go = new GameObject($"Player_View_{id}");

        // 【关键修改 2】：设置渲染层级 (OrderInLayer)
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100; // 设置一个较大的值，确保在地图（通常为0）之上

        // 如果你有多个 Sorting Layer，也可以指定名称
        // sr.sortingLayerName = "Character"; 

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