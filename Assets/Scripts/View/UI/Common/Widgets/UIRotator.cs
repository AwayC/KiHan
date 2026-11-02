using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 匀速旋转 (如大招就绪光圈)；节点隐藏时不运行
    /// </summary>
    public class UIRotator : MonoBehaviour
    {
        [Tooltip("每秒旋转角度 (正数逆时针)")]
        [SerializeField] private float _degreesPerSecond = -180f;

        private void Update()
        {
            transform.Rotate(0f, 0f, _degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
