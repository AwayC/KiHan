using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;
using KiHan.Asset;
using KiHan.DebugTools;
using KiHan.Logic;
using KiHan.Math;
using KiHan.Network;
using KiHan.View.Render;
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
        LockstepManager.Instance.Init(VirtualNetworkManager.Instance);
        VirtualNetworkManager.Instance.Connect();
    }
}

public class OnlineBattleFactory : IBattleFactory
{
    public void CreateBattle(uint roomId)
    {
        Debug.Log("[OnlineBattleFactory] 创建联机战斗...");
        var gateway = GatewayManager.Instance;
        if (gateway != null && gateway.IsAuthed)
        {
            KcpNetworkManager.Instance.Connect(gateway.ServerIp, (ushort)gateway.UdpPort, gateway.ConnId, gateway.ConnKey);
            LockstepManager.Instance.Init(KcpNetworkManager.Instance);
            var req = new EnterRoomReq { room_id = (int)roomId };
            KcpNetworkManager.Instance.SendMsg(2001, req.Serialize());
        }
        else
        {
            Debug.LogError("[OnlineBattleFactory] 联机模式启动失败：尚未登录网关");
        }
    }
}

/// <summary>
/// 战斗生命周期与网络帧驱动中心：
/// 开局 (GameStartNtf) 同步构建地图、逻辑世界、角色与视图，保证第一个逻辑帧到达前世界已就绪；
/// 每个逻辑帧驱动 BattleWorld，随后由 SceneManager 推进表现层
/// </summary>
public class BattleManager : UnitySingleton<BattleManager>
{
    private const string MapPath = "Maps/01/scen";
    private const string CombatUIPath = "UI/Button/Canvas";
    private const int DefaultCharacterId = 90001; // 鸣人

    // 双方出生点与槽位 (槽位即 RoomFrame.InputFrames 的 gameId)
    private const int P1Slot = 1;
    private const int P2Slot = 2;
    private static readonly FPVector3 P1Spawn = new FPVector3(FP.Parse("-2"), FP.Zero, FP.Zero);
    private static readonly FPVector3 P2Spawn = new FPVector3(FP.Parse("2"), FP.Zero, FP.Zero);

    public uint roomId = 1;
    public bool IsOnline = false;

    public BattleWorld World { get; private set; }

    private readonly List<CharacterObj> _characters = new List<CharacterObj>();
    private GameObject _combatUIGo;
    private int _p1CharacterId = DefaultCharacterId;
    private int _p2CharacterId = DefaultCharacterId;
    private bool _infiniteHp;

    public void EnterBattle(bool isOnline, uint targetRoomId = 1)
    {
        this.roomId = targetRoomId;
        this.IsOnline = isOnline;

        _p1CharacterId = DefaultCharacterId;
        _p2CharacterId = DefaultCharacterId;
        _infiniteHp = false;
        if (!isOnline)
        {
            DebugConfig dbgConfig = Resources.Load<DebugConfig>(DebugConfig.DefaultPath);
            if (dbgConfig != null)
            {
                // 无限血只用于单机：联机双方配置不同会导致不同步
                _infiniteHp = dbgConfig.infiniteHp;
                if (dbgConfig.isBattleDebug)
                {
                    _p1CharacterId = dbgConfig.debugPlayer1CharId;
                    _p2CharacterId = dbgConfig.debugPlayer2CharId;
                }
            }
        }

        // 绑定帧同步回调 (须在连接网络前，单机虚拟网络会同步派发开局消息)
        LockstepManager.Instance.OnExecuteFrame = OnBattleTick;
        LockstepManager.Instance.OnGameStart -= OnGameStart;
        LockstepManager.Instance.OnGameStart += OnGameStart;

        IBattleFactory factory = isOnline ? new OnlineBattleFactory() : (IBattleFactory)new OfflineBattleFactory();
        factory.CreateBattle(roomId);
    }

    public void OnGameStart()
    {
        Debug.Log("[BattleManager] 收到游戏开始信号，构建战斗...");
        LoadBattle();
    }

    private void LoadBattle()
    {
        MapManager.Instance.LoadMap(MapPath);
        World = new BattleWorld(MapManager.Instance.CurrentMapLogic);

        int localSlot = IsOnline ? LockstepManager.Instance.MyGameId : P1Slot;
        SceneManager.Instance.InitWorld(localSlot);

        ActorView p1 = SpawnPlayer(_p1CharacterId, P1Slot, P1Spawn, facingLeft: false);
        ActorView p2 = SpawnPlayer(_p2CharacterId, P2Slot, P2Spawn, facingLeft: true);
        ActorView local = localSlot == P2Slot ? p2 : p1;
        if (local != null) CameraControllor.Instance.SetTarget(local, true);

        InitCombatUI();
        UIManager.Instance.ClosePanel(UIConst.LobbyPanel);
        GameApp.Instance.SetGameRunning(true);
    }

    /// <summary>
    /// 玩家的 EntityId / 阵营 / 输入槽位均取槽位号，保证双端一致
    /// </summary>
    private ActorView SpawnPlayer(int characterId, int slot, FPVector3 position, bool facingLeft)
    {
        if (!CharacterFactory.IsKnown(characterId))
        {
            Debug.LogWarning($"[BattleManager] 角色 {characterId} 尚未接入新框架，改用默认角色 {DefaultCharacterId}");
            characterId = DefaultCharacterId;
        }

        CharacterObj character = CharacterFactory.Create(characterId, slot, slot, slot, position, facingLeft);
        if (character == null) return null;
        character.infiniteHp = _infiniteHp;

        World.AddCharacter(character);
        _characters.Add(character);
        return SceneManager.Instance.SpawnView(character);
    }

    private void InitCombatUI()
    {
        GameObject uiPrefab = AssetManager.Instance.Load<GameObject>(CombatUIPath);
        if (uiPrefab == null) return;

        _combatUIGo = Instantiate(uiPrefab);
        _combatUIGo.name = "Battle_UI";
        if (_combatUIGo.GetComponent<View.UI.BattleUIPanel>() == null)
        {
            _combatUIGo.AddComponent<View.UI.BattleUIPanel>();
        }
    }

    public void OnBattleTick(RoomFrame frame)
    {
        if (!GameApp.Instance.IsGameRunning || World == null) return;

        World.Tick(frame);
        SceneManager.Instance.OnLogicTick(World);
    }

    public void ExitBattle(Action onComplete)
    {
        GameApp.Instance.PerformTransitionAsync(ExitBattleRoutine(onComplete));
    }

    private IEnumerator ExitBattleRoutine(Action onFinished)
    {
        Debug.Log("[BattleManager] 正在清理战斗资源...");
        GameApp.Instance.SetGameRunning(false);
        LockstepManager.Instance.OnExecuteFrame = null;
        LockstepManager.Instance.StopGame();

        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();
        if (KcpNetworkManager.Instance != null && KcpNetworkManager.Instance.IsConnected)
        {
            KcpNetworkManager.Instance.Disconnect();
        }

        World = null;
        CameraControllor.Instance.ResetView();
        SceneManager.Instance.Clear();
        foreach (CharacterObj character in _characters)
        {
            if (character != null) Destroy(character.gameObject);
        }
        _characters.Clear();

        if (_combatUIGo != null)
        {
            Destroy(_combatUIGo);
            _combatUIGo = null;
        }

        MapManager.Instance.ClearMap();
        EffectManager.Instance.Clear();
        ResManager.Instance.Clear();

        yield return new WaitForSecondsRealtime(0.3f);
        onFinished?.Invoke();
    }
}
