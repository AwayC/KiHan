using System;
using System.Collections;
using System.Collections.Generic;
using KiHan.DebugTools;
using KiHan.Network;
using KiHan.UI;
using KiHan.Config;
using Managers;
using UnityEngine;

/// <summary>
/// 游戏流程：登录 → 大厅 → 战斗 → 大厅 / 登出。决定什么时候打开哪个界面、什么时候进出战斗；
/// 界面只调用这里的意图方法 (开始战斗、退出战斗、登出)，不直接调用 BattleManager 与网络清理。
/// 3v3 (人机 / 联机)：选人 → 转场 → 阵容 Loading → 战斗；回合内的播报与换人 Loading 由战斗 HUD 按对局阶段驱动
/// </summary>
public sealed class GameFlow : UnitySingleton<GameFlow>
{
    /// <summary>
    /// 3v3 阵容人数
    /// </summary>
    public const int LineupSize = 3;

    /// <summary>
    /// 战斗逻辑是否在运行 (BattleManager 只在运行时推进逻辑帧)
    /// </summary>
    public bool IsGameRunning { get; private set; }

    public void SetGameRunning(bool running) => IsGameRunning = running;

    /// <summary>
    /// 选人界面正在为哪种模式选择 (确认时据此开始人机对战或提交联机阵容)
    /// </summary>
    private BattleMode _prepareMode;

    /// <summary>
    /// 联机匹配成功、等待进房 / 选人 / 双方阵容的阶段
    /// </summary>
    private bool _onlinePending;

    private readonly System.Random _random = new System.Random();

    private void Start()
    {
        LockstepManager lockstep = LockstepManager.Instance;
        lockstep.OnRoomEntered += OnRoomEntered;
        lockstep.OnLineupReceived += OnLineupReceived;
        lockstep.OnServerGameOver += OnServerGameOver;
    }

    /// <summary>
    /// 启动：调试配置直接进训练场，否则进登录
    /// </summary>
    public void Boot()
    {
        DebugConfig dbg = KiHan.Asset.AssetManager.Instance.Load<DebugConfig>(DebugConfig.DefaultPath);
        if (dbg != null && dbg.isBattleDebug) StartTraining();
        else EnterLogin();
    }

    public void EnterLogin()
    {
        CloseScreens();
        UIManager.Instance.Open(UIPanels.Login);
    }

    public void EnterLobby()
    {
        CloseScreens();
        UIManager.Instance.Open(UIPanels.Lobby);
    }

    /// <summary>
    /// 单机训练场 (不走选人)
    /// </summary>
    public void StartTraining()
    {
        IsGameRunning = true;
        BattleManager.Instance.EnterTraining();
    }

    /// <summary>
    /// 人机对战：打开选人界面
    /// </summary>
    public void OpenVsAIPrepare()
    {
        _prepareMode = BattleMode.VsAI;
        UIManager.Instance.Open(UIPanels.Prepare, new PrepareArgs { Mode = BattleMode.VsAI });
    }

    /// <summary>
    /// 匹配成功：进入联机房间 (进房后打开选人界面，见 OnRoomEntered)
    /// </summary>
    public void StartOnlineMatch(uint roomId)
    {
        _onlinePending = true;
        BattleManager.Instance.EnterOnline(roomId);
    }

    /// <summary>
    /// 选人确认 (选人界面调用)：出战顺序的角色 id 与各自佩戴的秘卷 (空串为不佩戴)
    /// </summary>
    public void ConfirmLineup(int[] characterIds, string[] secrets)
    {
        PlayerLoadout[] lineup = ToLineup(characterIds, secrets);
        if (_prepareMode == BattleMode.Online)
        {
            var members = new List<LineupMember>(lineup.Length);
            foreach (PlayerLoadout loadout in lineup) members.Add(new LineupMember { character_id = loadout.characterId, secret = loadout.secret });
            LockstepManager.Instance.SubmitLineup(members);
            UIManager.Instance.Toast("等待对手确认阵容...");
            return;
        }

        PlayerLoadout[] ai = RandomLineup();
        PlayWipe(() =>
        {
            CloseScreens();
            UIManager.Instance.Open(UIPanels.Loading3v3, new BattleLoadingArgs { P1Lineup = lineup, P2Lineup = ai, LocalSlot = 1 });
            IsGameRunning = true;
            BattleManager.Instance.EnterVsAI(lineup, ai);
        });
    }

    /// <summary>
    /// 转场：occlusion 从左往右扫过整屏，盖满时执行 onCovered (切换界面)
    /// </summary>
    public void PlayWipe(Action onCovered)
    {
        var wipe = UIManager.Instance.Open(UIPanels.ScreenWipe);
        if (wipe == null)
        {
            onCovered?.Invoke();
            return;
        }
        wipe.Call("Play", onCovered);
    }

    private static PlayerLoadout[] ToLineup(int[] characterIds, string[] secrets)
    {
        var lineup = new PlayerLoadout[characterIds.Length];
        for (int i = 0; i < characterIds.Length; i++)
        {
            string secret = secrets != null && i < secrets.Length ? secrets[i] : null;
            lineup[i] = new PlayerLoadout { characterId = characterIds[i], secret = secret ?? string.Empty };
        }
        return lineup;
    }

    /// <summary>
    /// 人机阵容：从已启用的忍者里随机 3 个 (不足 3 个时允许重复)，不佩戴秘卷
    /// </summary>
    private PlayerLoadout[] RandomLineup()
    {
        var pool = new List<CharacterManifest>();
        CharacterRegistry.CollectEnabled(pool);
        var lineup = new PlayerLoadout[LineupSize];
        for (int i = 0; i < LineupSize; i++)
        {
            int pick = _random.Next(pool.Count);
            lineup[i] = new PlayerLoadout { characterId = pool[pick].id };
            if (pool.Count > LineupSize - i) pool.RemoveAt(pick);
        }
        return lineup;
    }

    private void OnRoomEntered()
    {
        if (!_onlinePending) return;
        _prepareMode = BattleMode.Online;
        CloseScreens();
        UIManager.Instance.Open(UIPanels.Prepare, new PrepareArgs { Mode = BattleMode.Online });
    }

    private void OnLineupReceived()
    {
        if (!_onlinePending) return;
        _onlinePending = false;
        BattleManager.Instance.ApplyOnlineLineups();
        PlayWipe(() =>
        {
            CloseScreens();
            UIManager.Instance.Open(UIPanels.Loading3v3, new BattleLoadingArgs
            {
                P1Lineup = BattleManager.Instance.P1Lineup,
                P2Lineup = BattleManager.Instance.P2Lineup,
                LocalSlot = LockstepManager.Instance.MyGameId,
            });
        });
    }

    /// <summary>
    /// 服务器裁定结束：本地对局已结束 (正在显示结算) 时由结算界面返回；否则 (如对方掉线) 直接回大厅
    /// </summary>
    private void OnServerGameOver(uint winnerUid)
    {
        KiHan.Logic.BattleMatch match = BattleManager.Instance.Match;
        if (match != null && match.Phase == KiHan.Logic.MatchPhase.Finished) return;
        UIManager.Instance.Toast("对局已结束");
        ExitBattle();
    }

    /// <summary>
    /// BattleManager 加载完战斗后调用：关闭大厅等界面，打开战斗 HUD；单机训练场再叠加训练场面板
    /// </summary>
    public void OnBattleLoaded(BattleHudArgs hud)
    {
        CloseScreens();
        UIManager.Instance.Open(UIPanels.BattleHud, hud);
        if (hud.Training != null) UIManager.Instance.Open(UIPanels.Training, hud);
        IsGameRunning = true;
    }

    /// <summary>
    /// 退出战斗 (返回按钮确认、整场结算、联机结束)：Loading 中清理战斗，回到大厅 (未登录网关时，即单机模式，回到登录)
    /// </summary>
    public void ExitBattle()
    {
        IsGameRunning = false;
        _onlinePending = false;
        UIManager.Instance.CloseLayer(UILayer.Hud);
        UIManager.Instance.CloseLayer(UILayer.Loading);
        KiHan.Network.GatewayManager gateway = KiHan.Network.GatewayManager.Instance;
        Action back = gateway != null && gateway.IsAuthed ? (Action)EnterLobby : EnterLogin;
        RunTransition(BattleManager.Instance.CleanupRoutine(), back);
    }

    /// <summary>
    /// 训练场换人 / 换秘卷 (换人弹窗确定)：Loading 中清理战斗并换掉该槽位的角色与秘卷重开，地图保持
    /// </summary>
    public void RestartTraining(int slot, int characterId, string secret)
    {
        IsGameRunning = false;
        UIManager.Instance.CloseLayer(UILayer.Hud);
        RunTransition(BattleManager.Instance.RestartTrainingRoutine(slot, characterId, secret));
    }

    /// <summary>
    /// 局内切换地图 (选图弹窗选择)：Loading 遮罩下替换场景。
    /// 只换表现层，逻辑帧照常推进 (暂停 IsGameRunning 会让本机跳过确认帧，联机不同步)
    /// </summary>
    public void ChangeMap(MapConfig map)
    {
        if (map == null || map == MapManager.Instance.CurrentMap) return;
        RunTransition(ChangeMapRoutine(map));
    }

    private static IEnumerator ChangeMapRoutine(MapConfig map)
    {
        yield return null;
        MapManager.Instance.ChangeScene(map);
    }

    /// <summary>
    /// 登出：Loading 中断开网络并清理，回到登录
    /// </summary>
    public void Logout()
    {
        RunTransition(LogoutRoutine(), EnterLogin);
    }

    /// <summary>
    /// 清理会话 (网关连接、Token、地图、虚拟网络、场景视图与资源缓存)，不切换界面
    /// </summary>
    public void ResetSession()
    {
        KiHan.Network.GatewayManager.Instance?.Disconnect();
        HttpClient.Instance.ClearToken();
        MapManager.Instance.ClearMap();
        if (VirtualNetworkManager.Instance != null) VirtualNetworkManager.Instance.Stop();
        ViewManager.Instance.ClearAll();
        ResManager.Instance.Clear();
    }

    private IEnumerator LogoutRoutine()
    {
        ResetSession();
        yield return new WaitForSecondsRealtime(0.5f);
    }

    /// <summary>
    /// 显示 Loading 执行切换流程，结束后关闭 Loading 并回调
    /// </summary>
    public void RunTransition(IEnumerator routine, Action onFinish = null)
    {
        StopAllCoroutines();
        StartCoroutine(Transition(routine, onFinish));
    }

    private IEnumerator Transition(IEnumerator routine, Action onFinish)
    {
        var loading = (LuaPanel)UIManager.Instance.Open(UIPanels.Loading);
        if (loading != null) loading.Call("SetProgress", 0f);
        yield return null;

        yield return AnimateLoadingProgress(loading, 0f, 0.45f, 0.12f);
        yield return routine;
        yield return AnimateLoadingProgress(loading, 0.45f, 1f, 0.18f);

        if (loading != null) loading.Call("SetProgress", 1f);
        yield return new WaitForSecondsRealtime(0.08f);

        UIManager.Instance.Close(UIPanels.Loading);
        onFinish?.Invoke();
    }

    private static IEnumerator AnimateLoadingProgress(LuaPanel loading, float from, float to, float duration)
    {
        if (loading == null || duration <= 0f)
        {
            if (loading != null) loading.Call("SetProgress", to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            loading.Call("SetProgress", Mathf.Lerp(from, to, t));
            yield return null;
        }
        loading.Call("SetProgress", to);
    }

    /// <summary>
    /// 关闭全屏界面与弹窗 (切换流程时)
    /// </summary>
    private static void CloseScreens()
    {
        UIManager.Instance.CloseLayer(UILayer.Popup);
        UIManager.Instance.CloseLayer(UILayer.Normal);
    }
}
