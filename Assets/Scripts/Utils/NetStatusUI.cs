using UnityEngine;
using UnityEngine.UI;

public class NetStatusUI : MonoBehaviour
{
    public Text statusText; // 在 Inspector 中拖入一个 Text 组件

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnected += HandleConnected;
            NetworkManager.Instance.OnDisconnected += HandleDisconnected;
            NetworkManager.Instance.OnConnectFailed += HandleConnectFailed;
            
            statusText.text = "Disconnected";
            statusText.color = Color.gray;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnected -= HandleConnected;
            NetworkManager.Instance.OnDisconnected -= HandleDisconnected;
            NetworkManager.Instance.OnConnectFailed -= HandleConnectFailed;
        }
    }

    private void HandleConnected()
    {
        statusText.text = "Connected";
        statusText.color = Color.green;
    }

    private void HandleDisconnected()
    {
        statusText.text = "Disconnected";
        statusText.color = Color.gray;
    }

    private void HandleConnectFailed(string reason)
    {
        statusText.text = "Connect Failed: " + reason;
        statusText.color = Color.red;
    }

    // 可以在这里加个测试按钮调用这个方法
    public void OnConnectBtnClick()
    {
        statusText.text = "Connecting...";
        statusText.color = Color.yellow;
        NetworkManager.Instance.Connect();
    }
}
