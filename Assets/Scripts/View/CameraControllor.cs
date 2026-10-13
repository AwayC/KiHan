using UnityEngine;
using KiHan.View.Render;

/// <summary>
/// 相机控制器：不旋转的正交相机 (沿 +Z 观察)，追踪角色视图、边界限制以及打击感反馈
/// </summary>
public class CameraControllor : UnitySingleton<CameraControllor>
{
    [Header("Tracking")]
    public ActorView Target;
    public float smoothSpeed = 2.5f;

    /// <summary>
    /// 画面中心的屏幕纵坐标 (世界 Y，见 BattleViewSpace.ToWorld)
    /// </summary>
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
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = BattleViewSpace.CameraDistance + BattleViewSpace.BackdropDistance + 20f;

        transform.rotation = Quaternion.identity;
        transform.position = BattleViewSpace.CameraPosition(0f, yOffset);
    }

    public void SetTarget(ActorView target, bool immediate = false)
    {
        Target = target;
        if (immediate && Target != null)
        {
            transform.position = BattleViewSpace.CameraPosition(DesiredX(), yOffset);
        }
    }

    /// <summary>
    /// 回到初始机位 (退出战斗时)
    /// </summary>
    public void ResetView()
    {
        Target = null;
        transform.position = BattleViewSpace.CameraPosition(0f, yOffset);
    }

    private float DesiredX()
    {
        float offset = Target.Character != null && Target.Character.FacingLeft ? -lookAheadDistance : lookAheadDistance;
        return Mathf.Clamp(Target.transform.position.x + offset, minX, maxX);
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
        if (Target == null) return;

        // 1. 追踪角色视图位置 (表现层插值后的位置，避免追踪逻辑位置导致画面相对抖动)，缓动跟随
        float x = Mathf.Lerp(transform.position.x, DesiredX(), Time.deltaTime * smoothSpeed);
        transform.position = BattleViewSpace.CameraPosition(x, yOffset);

        // 2. 处理时间驱动的打击感震屏
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
