using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    [Tooltip("0: 跟随相机移动(背景); 1: 保持不动(普通物体); >1: 反向移动")]
    public float ParallaxFactor = 0.5f; 

    private Transform _cameraTrans;
    private float _startCameraX;
    private float _startX;

    private void Start()
    {
        // 延迟获取，确保相机已经初始化
        if (CameraControllor.Instance != null)
        {
            _cameraTrans = CameraControllor.Instance.transform;
            _startCameraX = _cameraTrans.position.x;
            _startX = transform.position.x;
        }
    }

    private void LateUpdate()
    {
        if (_cameraTrans == null)
        {
            // 容错处理：如果在 Start 时没拿到，尝试重新获取
            if (CameraControllor.Instance != null)
            {
                _cameraTrans = CameraControllor.Instance.transform;
                _startCameraX = _cameraTrans.position.x;
                _startX = transform.position.x;
            }
            return;
        }

        // 计算相机相对于初始位置的位移
        float cameraDeltaX = _cameraTrans.position.x - _startCameraX;

        // 视差位移量：Factor 为 0 时完全跟随相机，为 1 时保持世界坐标不动
        // 我们通常设置背景的 Factor 在 0.1 ~ 0.9 之间
        float offsetX = cameraDeltaX * (1 - ParallaxFactor);

        transform.position = new Vector3(_startX + offsetX, transform.position.y, transform.position.z);
    }
}
