using System.Collections;
using System.Collections.Generic;
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
        // 关闭锁帧
        Application.targetFrameRate = 30;
        QualitySettings.vSyncCount = 0;

        // 初始化游戏框架代码
        // end

        // 初始化游戏逻辑模块代码
        if (gameObject.GetComponent<GameApp>() == null)
        {
            gameObject.AddComponent<GameApp>();
        }
        // end

        // 检查更新资源
        // end

        // 注意：不再这里直接调用 GameStart
        // 游戏启动将由 NetworkManager 连接成功后的协议流程触发
        // (RoomEnterResp -> PlayerReadyReq -> GameStartNtf -> GameApp.GameStart)
    }
}
