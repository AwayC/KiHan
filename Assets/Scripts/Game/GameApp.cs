using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;
using System;
using System.Collections;
using Managers;
using KiHan.View.UI.Login;

public class GameApp : UnitySingleton<GameApp>
{
    private uint _myUid;
    private bool _isGameRunning = false;
    public bool IsGameRunning => _isGameRunning;

    public void SetGameRunning(bool isRunning)
    {
        _isGameRunning = isRunning;
    }

    /// <summary>
    /// 通用异步场景切换接口
    /// </summary>
    public void PerformTransitionAsync(IEnumerator transitionRoutine, Action onFinish = null)
    {
        StopAllCoroutines();
        StartCoroutine(UnifiedTransition(transitionRoutine, onFinish));
    }

    private IEnumerator UnifiedTransition(IEnumerator transitionRoutine, Action onFinish)
    {
        // 1. 显示 Loading 界面
        GameObject loadingPrefab = Resources.Load<GameObject>("UI/Loading/Canvas");
        GameObject loadingGo = null;
        UnityEngine.UI.Slider slider = null;
        TMPro.TMP_Text percentText = null;
        
        if (loadingPrefab != null)
        {
            loadingGo = Instantiate(loadingPrefab);
            loadingGo.name = "LoadingUI";
            slider = loadingGo.GetComponentInChildren<UnityEngine.UI.Slider>(true);
            
            TMPro.TMP_Text[] texts = loadingGo.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name.ToLower().Contains("num") || t.gameObject.name.ToLower().Contains("text"))
                {
                    percentText = t;
                    break;
                }
            }
            
            Canvas canvas = loadingGo.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = 999;
        }

        Action<float> updateUI = (p) => {
            if (slider != null) slider.value = p;
            if (percentText != null) percentText.text = (p * 100f).ToString("F2") + "%";
        };

        updateUI(0f);
        yield return null;

        yield return transitionRoutine;

        updateUI(1.0f);
        yield return new WaitForSecondsRealtime(0.2f);

        if (loadingGo != null) Destroy(loadingGo);
        onFinish?.Invoke();
    }

    private void Start()
    {
        _myUid = (uint)(DateTime.Now.Ticks % 100000);
        Debug.Log($"[GameApp] 启动, UID: {_myUid}。正在初始化网络...");

        // 确保 Camera 存在，防止打包后场景无 Camera 导致 buffer trailing
        var _ = CameraControllor.Instance;

        // 启动 UI 框架与登录界面
        UIManager.Instance.OpenPanel<LoginPanel>(UIConst.LoginPanel);

        // --- 调试后门：启动即进入单机战斗 ---
        //StartOfflineGame();
    }

    public void StartOfflineGame()
    {
        Debug.Log("[GameApp] 路由：切入单机战斗状态...");
        _isGameRunning = true;
        
        // 核心：把具体的战斗加载与网络驱动，外包给 BattleManager
        BattleManager.Instance.EnterBattle(isOnline: false, targetRoomId: 1);
    }

    public void ExitGame()
    {
        _isGameRunning = false;
        BattleManager.Instance.ExitBattle(() => {
            // 回到大厅 UI
            UIManager.Instance.OpenPanel<KiHan.View.UI.Lobby.LobbyPanel>(UIConst.LobbyPanel);
        });
    }
}