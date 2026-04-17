using UnityEngine;

/// <summary>
/// 相机控制器，负责追踪目标和边界限制
/// </summary>
public class CameraControllor : UnitySingleton<CameraControllor>
{
    [Header("Tracking")]
    public Transform target;
    public float smoothSpeed = 5f;
    public float yOffset = 1.4f;

    [Header("Boundaries")]
    public float minX = -10f;
    public float maxX = 10f;

    private Camera _cam;

    protected override void Awake()
    {
        base.Awake();
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = gameObject.AddComponent<Camera>();
        
        // 初始化相机基本属性
        _cam.orthographic = true;
        _cam.orthographicSize = 3.0f;
        _cam.backgroundColor = Color.black;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetBoundaries(float min, float max)
    {
        minX = min;
        maxX = max;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 目标位置
        float targetX = Mathf.Clamp(target.position.x, minX, maxX);
        Vector3 targetPos = new Vector3(targetX, yOffset, -10f);

        // 平滑追踪
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
    }
}
