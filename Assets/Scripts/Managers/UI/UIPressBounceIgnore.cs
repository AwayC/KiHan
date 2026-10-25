using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 标记：该按钮有特殊的按压表现，BasePanel / UIPressBounce.AttachToButtons 自动挂载 Q 弹缩放时跳过它
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPressBounceIgnore : MonoBehaviour
    {
    }
}
