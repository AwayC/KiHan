using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameApp : UnitySingleton<GameApp>
{
    // 游戏逻辑入口
    public void GameStart()
    {
        Debug.Log("GameStart");
        this.EnterGame();
    }

    public void EnterGame()
    {
        // 加载游戏地图


        // 加载游戏逻辑地图



        // 加载游戏UI
    }
}