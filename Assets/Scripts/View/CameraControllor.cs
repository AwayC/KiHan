using UnityEngine;
using KiHan.Logic;

/// <summary>
/// 相机控制器，负责追踪逻辑目标、边界限制以及打击感反馈
/// </summary>
public class CameraControllor : UnitySingleton<CameraControllor>
{
    [Header("Tracking")]
    public LogicEntity TargetLogic; 
    public float smoothSpeed = 2.5f; 
    public float yOffset = 1.47f;
    public float lookAheadDistance = 3.0f; 

    [Header("Boundaries")]
    public float minX = -4.5f;
    public float maxX = 4.5f;

    private Camera _cam;
    private float _originalSize;

    // --- 打击感控制参数 ---
    private int _impactState = 0;       // 0: 空闲, 1: 保持放大, -1: 保持恢复
    private float _impactHoldTimer = 0f;  // 状态剩余持续时间
    private int _heavyHitCounter = 0;   // 剩余重击连震次数
    private float _currentZoom = 0f;

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
            
            // 尝试获取表现层位置
            float startX = TargetLogic.pos.x;
            var view = Managers.ViewManager.Instance.GetEntityView(TargetLogic);
            if (view != null) startX = view.transform.position.x;

            float targetX = Mathf.Clamp(startX + offset, minX, maxX);
            transform.position = new Vector3(targetX, yOffset, -10f);
        }
    }

    public void SetBoundaries(float min, float max)
    {
        minX = min;
        maxX = max;
    }

    /// <summary>
    /// 触发打击感反馈：瞬间放大（一帧）然后立即恢复
    /// </summary>
    public void ImpactEffect(bool isHeavyHit = false)
    {
        if (_originalSize <= 0) return;
        
        _heavyHitCounter = isHeavyHit ? 1 : 0; // 重击会额外再震动1次
        _currentZoom = isHeavyHit ? 0.05f : 0.05f; // 重击放大5%，普攻放大3%
        
        TriggerSingleImpact();
    }

    private void TriggerSingleImpact()
    {
        _impactState = 1;
        _impactHoldTimer = 0.04f; // 保持放大的渲染时间 (约 40ms)
        _cam.orthographicSize = _originalSize * (1f - _currentZoom);
    }

    private void LateUpdate()
    {
        if (TargetLogic == null) return;

        // 1. 获取表现层位置（避免摄像机追踪突变的逻辑位置导致画面相对抖动）
        float baseX = TargetLogic.pos.x;
        var view = Managers.ViewManager.Instance.GetEntityView(TargetLogic);
        if (view != null)
        {
            baseX = view.transform.position.x;
        }

        // 2. 计算目标位置
        float offset = TargetLogic.IsFacingLeft ? -lookAheadDistance : lookAheadDistance;
        float desiredX = baseX + offset;
        float targetX = Mathf.Clamp(desiredX, minX, maxX);
        Vector3 targetPos = new Vector3(targetX, yOffset, -10f);

        // 3. 缓动跟随
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);

        // 4. 处理时间驱动的打击感震屏
        if (_impactState == 1)
        {
            _impactHoldTimer -= Time.deltaTime;
            if (_impactHoldTimer <= 0)
            {
                // 瞬间恢复
                _cam.orthographicSize = _originalSize;
                
                if (_heavyHitCounter > 0)
                {
                    _heavyHitCounter--;
                    _impactState = -1;
                    _impactHoldTimer = 0.04f; // 两次震动之间的间隔时间
                }
                else
                {
                    _impactState = 0;
                }
            }
        }
        else if (_impactState == -1)
        {
            _impactHoldTimer -= Time.deltaTime;
            if (_impactHoldTimer <= 0)
            {
                // 间隔结束，触发第二次震动
                TriggerSingleImpact();
            }
        }
        else if (_originalSize > 0 && _cam.orthographicSize != _originalSize)
        {
            // 兜底恢复
            _cam.orthographicSize = _originalSize;
        }
    }
}
