using KiHan.View.Audio;
using UnityEngine;

/// <summary>
/// 启动入口：初始化常驻服务后交给 GameFlow (流程与界面切换都在 GameFlow 中)
/// </summary>
public class GameApp : UnitySingleton<GameApp>
{
    private void Start()
    {
        Debug.Log("[GameApp] 启动");

        // 确保 Camera 存在，防止打包后场景无 Camera 导致 buffer trailing
        var _ = CameraControllor.Instance;

        // 音频常驻 (首次访问即创建)：登录界面起即可播放按钮点击音
        AudioManager.Instance.LoadConventions();

        GameFlow.Instance.Boot();
    }
}
