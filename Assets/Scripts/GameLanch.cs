using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLanch : UnitySingleton<GameLanch>
{
    void Start()
    {
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
