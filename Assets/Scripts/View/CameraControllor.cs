using UnityEngine;

using KiHan.Logic;

/// <summary>
/// 相机控制器，负责追踪逻辑目标和边界限制
/// </summary>
public class CameraControllor : UnitySingleton<CameraControllor>
{
    [Header("Tracking")]
    public LogicEntity TargetLogic; 
    public float smoothSpeed = 2f; 
    public float yOffset = 1.47f;
    public float lookAheadDistance = 3.0f; // 领航距离，1.0 代表 100px。若需 1px 请设为 0.01

    [Header("Boundaries")]
    public float minX = -4.5f;
    public float maxX = 4.5f;

    private Camera _cam;

    protected override void Awake()
    {
        base.Awake();
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = gameObject.AddComponent<Camera>();
        
        gameObject.tag = "MainCamera"; // 设置主相机标签
        
        _cam.orthographic = true;
        _cam.orthographicSize = 2.8f;
        _cam.backgroundColor = Color.black;

        transform.position = new Vector3(0, yOffset, -100f);
    }

    public void SetTarget(LogicEntity logic, bool immediate = false)
    {
        TargetLogic = logic;
        if (immediate && TargetLogic != null)
        {
            // 初始也应用领航偏移
            float offset = TargetLogic.IsFacingLeft ? -lookAheadDistance : lookAheadDistance;
            float targetX = Mathf.Clamp(TargetLogic.pos.x + offset, minX, maxX);
            transform.position = new Vector3(targetX, yOffset, -10f);
        }
    }

    public void SetBoundaries(float min, float max)
    {
        minX = min;
        maxX = max;
    }

    private void LateUpdate()
    {
        if (TargetLogic == null) return;

        // 1. 计算领航目标位置
        // 如果人物面朝左(IsFacingLeft=true)，相机目标点向左偏；反之向右偏
        float offset = TargetLogic.IsFacingLeft ? -lookAheadDistance : lookAheadDistance;
        float desiredX = TargetLogic.pos.x + offset;

        // 2. 自动适应边界 (Clamp)
        float targetX = Mathf.Clamp(desiredX, minX, maxX);
        Vector3 targetPos = new Vector3(targetX, yOffset, -10f);

        // 3. 缓动跟随 (Lerp)
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
    }
}
