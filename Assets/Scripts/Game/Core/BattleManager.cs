using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;
using KiHan.Asset;
using KiHan.Config;
using KiHan.DebugTools;
using KiHan.Logic;
using KiHan.Logic.AI;
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
        // 本地服务器收到输入立即回送确认帧：不需要输入延迟
        LockstepManager.Instance.Init(VirtualNetworkManager.Instance, inputDelayFrames: 0);
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
            LockstepManager.Instance.Init(KcpNetworkManager.Instance, LockstepManager.OnlineInputDelayFrames);
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
/// 战斗模式
/// </summary>
public enum BattleMode
{
    /// <summary>单机训练场 (1v1，不分回合，训练面板)</summary>
    Training,
    /// <summary>单机人机对战 (3v3 车轮战，P2 由人机控制)</summary>
    VsAI,
    /// <summary>联机对战 (3v3 车轮战，阵容由服务器交换)</summary>
    Online,
}

/// <summary>
/// 战斗生命周期与网络帧驱动中心：
/// 开局 (GameStartNtf) 同步构建地图、逻辑世界、角色与视图，保证第一个逻辑帧到达前世界已就绪；
/// 每个逻辑帧驱动 BattleWorld 与对局 (BattleMatch)，随后由 SceneManager 推进表现层。
/// 3v3 (人机 / 联机)：加载时预建双方阵容全部角色的模板与特效；对局在换人帧给出换人请求，由这里替换出战角色 (Tick 之后执行，双端一致)
/// </summary>
public class BattleManager : UnitySingleton<BattleManager>
{
    private const int DefaultCharacterId = 90001; // 鸣人

    // 双方出生点与槽位 (槽位即 RoomFrame.InputFrames 的 gameId)
    private const int P1Slot = 1;
    private const int P2Slot = 2;
    private static readonly FPVector3 P1Spawn = new FPVector3(FP.Parse("-2"), FP.Zero, FP.Zero);
    private static readonly FPVector3 P2Spawn = new FPVector3(FP.Parse("2"), FP.Zero, FP.Zero);

    public uint roomId = 1;

    public BattleMode Mode { get; private set; }

    public bool IsOnline => Mode == BattleMode.Online;

    public BattleWorld World { get; private set; }

    /// <summary>
    /// 3v3 对局 (训练场为 null)
    /// </summary>
    public BattleMatch Match { get; private set; }

    /// <summary>
    /// 双方阵容 (按出战顺序；训练场各一个)
    /// </summary>
    public PlayerLoadout[] P1Lineup { get; private set; } = Array.Empty<PlayerLoadout>();
    public PlayerLoadout[] P2Lineup { get; private set; } = Array.Empty<PlayerLoadout>();

    /// <summary>
    /// 双方当前出战角色
    /// </summary>
    public CharacterObj P1 => _p1;
    public CharacterObj P2 => _p2;

    /// <summary>
    /// 出战角色变化次数 (换人后加 1；界面据此重新绑定角色状态与按键)
    /// </summary>
    public int RosterVersion { get; private set; }

    /// <summary>
    /// 本机玩家的输入槽位 (单机为 P1)
    /// </summary>
    public int LocalSlot { get; private set; } = P1Slot;

    /// <summary>
    /// 本机玩家控制的角色 (UI 读取技能 CD 与查克拉)
    /// </summary>
    public CharacterObj LocalCharacter { get; private set; }

    /// <summary>
    /// 单机训练场 (其他模式为 null)：训练面板的开关与对敌人 (P2) 的操控
    /// </summary>
    public TrainingSession Training { get; private set; }

    private readonly List<CharacterObj> _characters = new List<CharacterObj>();
    private SpawnPool _spawnPool;
    private PlayerLoadout _p1Loadout = new PlayerLoadout { characterId = DefaultCharacterId };
    private PlayerLoadout _p2Loadout = new PlayerLoadout { characterId = DefaultCharacterId };
    private CharacterObj _p1;
    private CharacterObj _p2;
    private HpMode _hpMode;
    private IBotBrain _bot;
    private bool _finishReported;

    /// <summary>
    /// 开局地图 (训练场换人重开时沿用当前地图)；为 null 时用地图注册表的默认地图
    /// </summary>
    private MapConfig _startMap;

    /// <summary>
    /// 单机训练场：调试配置开启时使用其中的出战配置与无限血
    /// </summary>
    public void EnterTraining()
    {
        Mode = BattleMode.Training;
        roomId = 1;
        _p1Loadout = new PlayerLoadout { characterId = DefaultCharacterId };
        _p2Loadout = new PlayerLoadout { characterId = DefaultCharacterId };
        _hpMode = HpMode.Normal;
        DebugConfig dbgConfig = AssetManager.Instance.Load<DebugConfig>(DebugConfig.DefaultPath);
        if (dbgConfig != null)
        {
            // 无限血只用于单机：联机双方配置不同会导致不同步
            if (dbgConfig.infiniteHp) _hpMode = HpMode.Refill;
            if (dbgConfig.isBattleDebug)
            {
                _p1Loadout = dbgConfig.player1;
                _p2Loadout = dbgConfig.player2;
            }
        }

        _startMap = null;
        StartBattle();
    }

    /// <summary>
    /// 单机人机对战 (3v3)：P2 阵容由调用方给出 (人机随机)
    /// </summary>
    public void EnterVsAI(PlayerLoadout[] p1Lineup, PlayerLoadout[] p2Lineup)
    {
        Mode = BattleMode.VsAI;
        roomId = 1;
        P1Lineup = p1Lineup;
        P2Lineup = p2Lineup;
        _hpMode = HpMode.Normal;
        _startMap = null;
        StartBattle();
    }

    /// <summary>
    /// 联机对战：进房 (EnterRoomRsp 后由选人界面提交阵容)，双方阵容由服务器广播 (LineupNtf)，开局 (GameStartNtf) 时加载
    /// </summary>
    public void EnterOnline(uint targetRoomId)
    {
        Mode = BattleMode.Online;
        roomId = targetRoomId;
        P1Lineup = Array.Empty<PlayerLoadout>();
        P2Lineup = Array.Empty<PlayerLoadout>();
        _hpMode = HpMode.Normal;
        _startMap = null;
        StartBattle();
    }

    /// <summary>
    /// 训练场换人 / 换秘卷 (GameFlow.RestartTraining 在 Loading 中执行)：清理当前战斗后换掉该槽位的角色与秘卷重开单机战斗，
    /// 地图、另一方配置与血量模式保持；训练面板的开关回到默认
    /// </summary>
    public IEnumerator RestartTrainingRoutine(int slot, int characterId, string secret)
    {
        // 出战配置可能直接引用调试配置资源：换人时复制一份，不改资源
        PlayerLoadout p1 = slot == P1Slot ? WithCharacterAndSecret(_p1Loadout, characterId, secret) : _p1Loadout;
        PlayerLoadout p2 = slot == P2Slot ? WithCharacterAndSecret(_p2Loadout, characterId, secret) : _p2Loadout;
        MapConfig map = MapManager.Instance.CurrentMap;
        HpMode hpMode = _hpMode;

        yield return CleanupRoutine();

        Mode = BattleMode.Training;
        _p1Loadout = p1;
        _p2Loadout = p2;
        _hpMode = hpMode;
        _startMap = map;
        StartBattle();
    }

    /// <summary>
    /// 该槽位当前佩戴的秘卷 ID (没有为 null)
    /// </summary>
    public string SecretOf(int slot)
    {
        PlayerLoadout loadout = slot == P2Slot ? _p2Loadout : _p1Loadout;
        return loadout != null ? loadout.secret : null;
    }

    /// <summary>
    /// 该槽位当前出战角色的出战配置 (训练场为该槽位配置；3v3 为阵容中当前出战者)
    /// </summary>
    public PlayerLoadout CurrentLoadout(int slot)
    {
        if (Match == null) return slot == P2Slot ? _p2Loadout : _p1Loadout;
        PlayerLoadout[] lineup = slot == P2Slot ? P2Lineup : P1Lineup;
        int index = slot == P2Slot ? Match.P2Index : Match.P1Index;
        return index >= 0 && index < lineup.Length ? lineup[index] : null;
    }

    /// <summary>
    /// 镜头回到画面中间再移向本机角色 (每回合开场，由界面在转场盖住画面时调用；只影响表现)
    /// </summary>
    public void FocusCameraFromCenter()
    {
        CameraControllor.Instance.ResetView();
        ActorView local = LocalCharacter != null ? ViewManager.Instance.GetView(LocalCharacter.EntityId) : null;
        if (local != null) CameraControllor.Instance.SetTarget(local, false);
    }

    private static PlayerLoadout WithCharacterAndSecret(PlayerLoadout loadout, int characterId, string secret)
    {
        return new PlayerLoadout { characterId = characterId, secret = secret, summons = loadout != null ? loadout.summons : null };
    }

    /// <summary>
    /// 绑定帧同步回调并创建战斗 (单机虚拟网络会同步派发开局消息，开局即 LoadBattle)
    /// </summary>
    private void StartBattle()
    {
        // 绑定帧同步回调 (须在连接网络前，单机虚拟网络会同步派发开局消息)
        LockstepManager.Instance.OnExecuteFrame = OnBattleTick;
        LockstepManager.Instance.OnGameStart -= OnGameStart;
        LockstepManager.Instance.OnGameStart += OnGameStart;

        IBattleFactory factory = IsOnline ? new OnlineBattleFactory() : (IBattleFactory)new OfflineBattleFactory();
        factory.CreateBattle(roomId);
    }

    public void OnGameStart()
    {
        Debug.Log("[BattleManager] 收到游戏开始信号，构建战斗...");
        LoadBattle();
    }

    private void LoadBattle()
    {
        if (IsOnline) ApplyOnlineLineups();

        MapManager.Instance.LoadMap(_startMap != null ? _startMap : MapRegistry.Default);
        World = new BattleWorld(MapManager.Instance.CurrentMapLogic);
        _spawnPool = new SpawnPool();
        World.Spawner = _spawnPool;
        // 秘卷的 Buff 与角色无关，开局全部登记 (角色的 Buff 在生成角色时登记)
        var scrollBuffs = new List<BuffInfo>();
        SecretScrollRegistry.CollectBuffs(scrollBuffs);
        foreach (BuffInfo buff in scrollBuffs) World.BuffTable[buff.id] = buff;

        LocalSlot = IsOnline ? LockstepManager.Instance.MyGameId : P1Slot;
        SceneManager.Instance.InitWorld(LocalSlot);

        Match = null;
        Training = null;
        _bot = null;
        _finishReported = false;
        if (Mode != BattleMode.Training)
        {
            // 3v3：双方阵容全部角色的模板、生成物与特效在加载阶段预建 (换人时不再加载资源)
            foreach (PlayerLoadout loadout in P1Lineup) Prewarm(loadout.characterId);
            foreach (PlayerLoadout loadout in P2Lineup) Prewarm(loadout.characterId);
            Match = new BattleMatch(Mathf.Min(P1Lineup.Length, P2Lineup.Length));
            _p1Loadout = P1Lineup[0];
            _p2Loadout = P2Lineup[0];
            if (Mode == BattleMode.VsAI) _bot = MeleeBotBrain.Create();
        }

        ActorView p1 = SpawnPlayer(_p1Loadout, P1Slot, P1Spawn, facingLeft: false);
        ActorView p2 = SpawnPlayer(_p2Loadout, P2Slot, P2Spawn, facingLeft: true);
        _p1 = p1 != null ? p1.Character : null;
        _p2 = p2 != null ? p2.Character : null;
        ActorView local = LocalSlot == P2Slot ? p2 : p1;
        LocalCharacter = local != null ? local.Character : null;
        if (local != null) CameraControllor.Instance.SetTarget(local, true);
        if (Mode == BattleMode.Training && _p1 != null && _p2 != null) Training = new TrainingSession(_p1, _p2, _hpMode, MeleeBotBrain.Create());
        RosterVersion++;

        // 生成物实例与视图、动作事件特效全部在加载阶段预建 (局内不再 Load / 新建实例)
        SceneManager.Instance.PrewarmSpawnViews(_spawnPool.Instances);

        PlayerLoadout localLoadout = LocalSlot == P2Slot ? _p2Loadout : _p1Loadout;
        GameFlow.Instance.OnBattleLoaded(new KiHan.UI.BattleHudArgs { Mode = Mode, P1 = _p1, P2 = _p2, Local = LocalCharacter, LocalLoadout = localLoadout, Training = Training });
    }

    /// <summary>
    /// 联机阵容 (LineupNtf)：game_id 1 / 2 对应 P1 / P2；缺失时用默认角色补齐并报错
    /// </summary>
    public void ApplyOnlineLineups()
    {
        P1Lineup = ToLoadouts(P1Slot);
        P2Lineup = ToLoadouts(P2Slot);
    }

    private static PlayerLoadout[] ToLoadouts(int gameId)
    {
        if (!LockstepManager.Instance.Lineups.TryGetValue(gameId, out List<LineupMember> members) || members.Count == 0)
        {
            Debug.LogError($"[BattleManager] 没有收到 {gameId}P 的阵容 (LineupNtf)，使用默认角色");
            return new[] { new PlayerLoadout { characterId = DefaultCharacterId } };
        }
        var result = new PlayerLoadout[members.Count];
        for (int i = 0; i < members.Count; i++) result[i] = new PlayerLoadout { characterId = members[i].character_id, secret = members[i].secret ?? string.Empty };
        return result;
    }

    /// <summary>
    /// 同一角色的生成物实例、Buff 配置与动作 / Buff 特效只预建一次
    /// </summary>
    private void Prewarm(int characterId)
    {
        if (!CharacterFactory.IsKnown(characterId)) characterId = DefaultCharacterId;
        if (!_spawnPool.Prewarm(characterId)) return;
        CharacterTemplate template = CharacterFactory.GetTemplate(characterId);
        foreach (BuffInfo buff in template.Buffs.Values) World.BuffTable[buff.id] = buff;
        SceneManager.Instance.PreloadActionEffects(template);
    }

    /// <summary>
    /// 玩家的 EntityId / 阵营 / 输入槽位均取槽位号，保证双端一致
    /// </summary>
    private ActorView SpawnPlayer(PlayerLoadout loadout, int slot, FPVector3 position, bool facingLeft)
    {
        int characterId = loadout.characterId;
        if (!CharacterFactory.IsKnown(characterId))
        {
            Debug.LogWarning($"[BattleManager] 角色 {characterId} 尚未接入新框架，改用默认角色 {DefaultCharacterId}");
            characterId = DefaultCharacterId;
        }

        CharacterObj character = CharacterFactory.Create(characterId, slot, slot, slot, position, facingLeft, loadout.secret);
        if (character == null) return null;
        character.hpMode = _hpMode;

        World.AddCharacter(character);
        _characters.Add(character);
        Prewarm(character.CharacterId);
        return SceneManager.Instance.SpawnView(character);
    }

    /// <summary>
    /// 人机 / 训练场敌人的本帧输入 (虚拟网络打包 P2 输入帧时调用)
    /// </summary>
    public void FillBotInput(uint frameId, InputFrame frame)
    {
        if (Training != null)
        {
            Training.FillDummyInput(frameId, frame);
            return;
        }
        if (_bot == null || _p2 == null || _p1 == null) return;
        _bot.Think(_p2, _p1, frameId, frame);
    }

    public void OnBattleTick(RoomFrame frame)
    {
        if (!GameFlow.Instance.IsGameRunning || World == null) return;

        if (Match != null) World.InputLocked = Match.InputLocked;
        bool teleported = Training != null && Training.BeforeTick();
        World.Tick(frame);
        Training?.AfterTick(World);
        if (Match != null) TickMatch();
        SceneManager.Instance.OnLogicTick(World);

        // 训练场瞬移：敌人视图直接对齐到新位置，不从旧位置插值滑过去
        if (teleported) ViewManager.Instance.GetView(_p2.EntityId)?.SnapToLogic();
    }

    private void TickMatch()
    {
        Match.Tick(_p1, _p2);
        if (Match.TakeSwap(out bool swapP1, out bool swapP2)) SwapFighters(swapP1, swapP2);
        if (Match.Phase == MatchPhase.Finished && !_finishReported)
        {
            _finishReported = true;
            int winner = Match.Result == MatchResult.P1Win ? P1Slot : Match.Result == MatchResult.P2Win ? P2Slot : 0;
            if (IsOnline) LockstepManager.Instance.ReportGameOver(winner, HpOf(_p1), HpOf(_p2));
        }
    }

    private static int HpOf(CharacterObj character) => character != null ? (int)character.Hp : 0;

    /// <summary>
    /// 回合换人 (对局换人帧，Tick 之后执行)：清掉全部生成物；负者 (平局双方) 换成阵容中的下一个 (继承其剩余查克拉)，胜者回到出生点并保留状态
    /// </summary>
    private void SwapFighters(bool swapP1, bool swapP2)
    {
        World.ClearSpawns();
        if (swapP1) _p1 = ReplaceFighter(_p1, P1Lineup[Match.P1Index], P1Slot, P1Spawn, false);
        else _p1?.ResetForRound(P1Spawn, false);
        if (swapP2) _p2 = ReplaceFighter(_p2, P2Lineup[Match.P2Index], P2Slot, P2Spawn, true);
        else _p2?.ResetForRound(P2Spawn, true);

        _p1Loadout = P1Lineup[Match.P1Index];
        _p2Loadout = P2Lineup[Match.P2Index];
        LocalCharacter = LocalSlot == P2Slot ? _p2 : _p1;
        _bot?.Reset();
        RosterVersion++;

        // 留场角色的视图直接对齐到出生点，不从旧位置滑过去
        foreach (CharacterObj fighter in new[] { _p1, _p2 })
        {
            if (fighter != null) ViewManager.Instance.GetView(fighter.EntityId)?.SnapToLogic();
        }
    }

    private CharacterObj ReplaceFighter(CharacterObj old, PlayerLoadout next, int slot, FPVector3 position, bool facingLeft)
    {
        int chakra = -1;
        if (old != null)
        {
            if (old.Skills != null) chakra = old.Skills.Chakra;
            World.RemoveCharacter(old);
            _characters.Remove(old);
            ActorView oldView = ViewManager.Instance.Unregister(old.EntityId);
            if (oldView != null)
            {
                oldView.Release();
                Destroy(oldView.gameObject);
            }
            Destroy(old.gameObject);
        }
        ActorView view = SpawnPlayer(next, slot, position, facingLeft);
        CharacterObj fighter = view != null ? view.Character : null;
        // 查克拉随阵容继承 (新角色查克拉上限不同时按其上限截断)
        if (fighter != null && fighter.Skills != null && chakra >= 0) fighter.Skills.SetChakra(chakra);
        return fighter;
    }

    /// <summary>
    /// 清理战斗 (GameFlow.ExitBattle 在 Loading 中执行)：停止帧同步与网络，销毁角色、场景视图、地图与特效
    /// </summary>
    public IEnumerator CleanupRoutine()
    {
        Debug.Log("[BattleManager] 正在清理战斗资源...");
        GameFlow.Instance.SetGameRunning(false);
        LockstepManager.Instance.OnExecuteFrame = null;
        LockstepManager.Instance.StopGame();

        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();
        if (KcpNetworkManager.Instance != null && KcpNetworkManager.Instance.IsConnected)
        {
            KcpNetworkManager.Instance.Disconnect();
        }

        World = null;
        Match = null;
        LocalCharacter = null;
        Training = null;
        _bot = null;
        _p1 = null;
        _p2 = null;
        CameraControllor.Instance.ResetView();
        SceneManager.Instance.Clear();
        foreach (CharacterObj character in _characters)
        {
            if (character != null) Destroy(character.gameObject);
        }
        _characters.Clear();
        _spawnPool?.Dispose();
        _spawnPool = null;
        CharacterFactory.ClearTemplates();

        MapManager.Instance.ClearMap();
        EffectManager.Instance.Clear();
        ResManager.Instance.Clear();

        yield return new WaitForSecondsRealtime(0.3f);
    }
}
