using UnityEngine;
using KiHan.Logic;

/// <summary>
/// 相机控制器，负责追踪逻辑目标、边界限制以及打击感反馈
/// </summary>
public class CameraControllor : UnitySingleton<CameraControllor>
{
    [Header("Tracking")]
    public LogicEntity TargetLogic; 
    public float smoothSpeed = 4f; 
    public float yOffset = 1.47f;
    public float lookAheadDistance = 3.0f; 

    [Header("Boundaries")]
    public float minX = -4.5f;
    public float maxX = 4.5f;

    private Camera _cam;
    private float _impactTimer;
    private float _originalSize;

    protected override void Awake()
    {
        base.Awake();
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = gameObject.AddComponent<Camera>();
        
        gameObject.tag = "MainCamera";
        
        _cam.orthographic = true;
        _cam.orthographicSize = 2.8f;
        _originalSize = _cam.orthographicSize;
        _cam.backgroundColor = Color.black;
        _cam.farClipPlane = 1000f; 

        transform.position = new Vector3(0, yOffset, -10f);
    }

    public void SetTarget(LogicEntity logic, bool immediate = false)
    {
        TargetLogic = logic;
        if (immediate && TargetLogic != null)
        {
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

    /// <summary>
    /// 触发打击感反馈：瞬间放大（拉近） + 平滑缩回
    /// </summary>
    /// <param name="zoomPercent">放大比例 (0.05 代表放大 5%)</param>
    /// <param name="duration">回弹时间</param>
    public void ImpactEffect(float zoomPercent = 0.03f, float duration = 0.1f)
    {
        if (_originalSize <= 0) return;
        
        _impactTimer = duration;
        // 瞬间减小 orthographicSize = 画面放大
        _cam.orthographicSize = _originalSize * (1f - zoomPercent);
    }

    private void LateUpdate()
    {
        if (TargetLogic == null) return;

        // 1. 计算基础目标位置
        float offset = TargetLogic.IsFacingLeft ? -lookAheadDistance : lookAheadDistance;
        float desiredX = TargetLogic.pos.x + offset;
        float targetX = Mathf.Clamp(desiredX, minX, maxX);
        Vector3 targetPos = new Vector3(targetX, yOffset, -10f);

        // 2. 缓动跟随
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);

        // 3. 处理打击感缩放回弹
        if (_impactTimer > 0)
        {
            _impactTimer -= Time.deltaTime;
            // 平滑缩回到原始大小
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _originalSize, Time.deltaTime * 12f);
        }
        else if (_originalSize > 0 && _cam.orthographicSize != _originalSize)
        {
            _cam.orthographicSize = _originalSize;
        }
    }
}
