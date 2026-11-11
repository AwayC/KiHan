using System.Collections;
using KiHan.Asset;
using KiHan.HotUpdate;
using UnityEngine;

public class GameLanch : UnitySingleton<GameLanch>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        Debug.Log("[GameLanch] 自动触发初始化流程...");
        var instance = GameLanch.Instance;
    }

    void Start()
    {
        Debug.Log("[GameLanch] 启动并挂载逻辑模块...");
        // 渲染锁 30 帧 (逻辑帧由 LockstepManager 的定时器按真实时间驱动，与渲染帧率无关；渲染帧率可按画面需要调高)
#if UNITY_EDITOR
        EditorFramePacer.Install(KiHan.Logic.GameConfig.LOGIC_FPS);
#else
        Application.targetFrameRate = KiHan.Logic.GameConfig.LOGIC_FPS;
#endif
        QualitySettings.vSyncCount = 0;

        StartCoroutine(Boot());
    }

    /// <summary>
    /// 启动顺序：热更 (AB 模式) → 资源提供器 → 游戏逻辑模块
    /// </summary>
    private IEnumerator Boot()
    {
        if (AssetMode.UseBundles)
        {
            var view = HotUpdateView.Create();
            var updater = new HotUpdater();
            yield return updater.Run(view);
            if (updater.Manifest == null) yield break; // ShowFatal 已显示
            view.Close();
            AssetManager.Instance.SetProvider(new BundleAssetProvider(updater.Manifest));
        }

        KiHan.Lua.LuaManager.Instance.Init();
        if (gameObject.GetComponent<GameApp>() == null) gameObject.AddComponent<GameApp>();
    }
}
