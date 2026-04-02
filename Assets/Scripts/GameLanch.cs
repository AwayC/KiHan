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
        this.gameObject.AddComponent<GameApp>();
        // end

        // 检查更新资源
        // end

        // 初始化游戏逻辑入口
        GameApp.Instance.GameStart();
        // end
    }
}
