using UnityEngine;
using KiHan.Logic;
using KiHan.View.Cinematic;
using KiHan.View.Render;

/// <summary>
/// 相机控制器：正交相机 (沿 +Z 观察)，技能 / 演出时可绕视线轴旋转
/// 每帧分层合成：跟随 -> 动作镜头 (Camera shot 指令) -> 演出镜头 (Timeline CameraShotTrack) -> 边界 -> 旋转补偿 -> 缩放脉冲 -> 晃动
/// 所有镜头效果都限制在正常跟随的取景范围内 (minX / maxX 与正常画面上下沿)，推近后可在该范围内移动，不会露出范围外
/// 晚于角色视图更新 (读取本帧插值后的角色位置)
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraControllor : UnitySingleton<CameraControllor>
{
    [Header("Tracking")]
    public ActorView Target;
    public float smoothSpeed = 2.5f;

    /// <summary>
    /// 画面中心的屏幕纵坐标 (世界 Y，见 BattleViewSpace.ToWorld)；取景参数随地图按 BattleViewSpace.MapScale 放大
    /// </summary>
    public float yOffset = 1.47f * BattleViewSpace.MapScale;
    public float lookAheadDistance = 3.0f * BattleViewSpace.MapScale;

    [Header("Boundaries")]
    public float minX = -4.5f * BattleViewSpace.MapScale;
    public float maxX = 4.5f * BattleViewSpace.MapScale;

    /// <summary>
    /// 镜头缩放 (正交尺寸倍率，> 1 拉远) 的下限 / 上限与屏幕内旋转角上限 (度)
    /// 拉远上限按现有地图实测：拉远时画面下沿不动，上沿 = yOffset + 正交尺寸 × (2 × zoom - 1)；
    /// 01 号地图背景上沿 5.97 (允许约 1.16)，02 号地图上沿 5.01 (原取景上沿 4.88，按 0.05 步进只有 1.0 不露黑底)，
    /// 因此暂为 1 (不拉远)；地图背景加高后再上调
    /// </summary>
    public const float MinZoom = 0.4f;
    public const float MaxZoomOut = 1f;
    public const float MaxRoll = 30f;

    /// <summary>
    /// 打击冲击 (impact / impact_heavy)：推近 5%，保持 40ms，重击间隔 40ms 再来一次
    /// </summary>
    private const float ImpactZoom = 0.95f;
    private const float ImpactHold = 0.04f;

    private Camera _cam;
    private float _originalSize;

    /// <summary>
    /// 跟随结果 (最终位置叠加了镜头效果，不能回读 transform)
    /// </summary>
    private float _followX;

    // --- 动作镜头 (CameraEventPlayer 每帧写入) ---
    private Vector2 _actionCenter;
    private float _actionZoom = 1f;
    private float _actionRoll;
    private float _actionWeight;

    // --- 演出镜头 (CameraShotTrack 每帧写入) ---
    private Vector2 _shotCenter;
    private float _shotZoom = 1f;
    private float _shotRoll;
    private float _shotWeight;

    // --- 演出晃动 (CameraShakeTrack 每帧写入) ---
    private Vector2 _cinematicShakeOffset;
    private float _cinematicShakeRoll;
    private float _cinematicShakeAmplitude;
    private float _cinematicShakeRollAmplitude;

    // --- 晃动指令 (上下左右，振幅线性衰减) ---
    private bool _shaking;
    private int _shakeId;
    private float _shakeAmplitude;
    private float _shakeFrequency;
    private float _shakeRollAmplitude;
    private float _shakeDuration;
    private float _shakeTime;

    // --- 缩放脉冲 (pulse 指令 / 打击冲击) ---
    private bool _pulsing;
    private int _pulseId;
    private float _pulseZoom = 1f;
    private float _pulseIn;
    private float _pulseHold;
    private float _pulseOut;
    private float _pulseInterval;
    private int _pulseCount;
    private CameraEase _pulseEase;
    private float _pulseTime;

    private int _nextEffectId;

    protected override void Awake()
    {
        base.Awake();
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = gameObject.AddComponent<Camera>();
        if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

        gameObject.tag = "MainCamera";

        _cam.orthographic = true;
        _cam.orthographicSize = 2.8f * BattleViewSpace.MapScale;
        _originalSize = _cam.orthographicSize;
        _cam.backgroundColor = Color.black;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = BattleViewSpace.CameraDistance + BattleViewSpace.BackdropDistance + 20f;

        transform.rotation = Quaternion.identity;
        transform.position = BattleViewSpace.CameraPosition(0f, yOffset);
        _followX = 0f;
    }

    public void SetTarget(ActorView target, bool immediate = false)
    {
        Target = target;
        if (immediate && Target != null)
        {
            _followX = DesiredX();
            transform.position = BattleViewSpace.CameraPosition(_followX, yOffset);
        }
    }

    /// <summary>
    /// 回到初始机位 (退出战斗时)，清除全部镜头效果
    /// </summary>
    public void ResetView()
    {
        Target = null;
        ClearCinematic();
        ClearActionShot();
        _shaking = false;
        _pulsing = false;
        _followX = 0f;
        transform.position = BattleViewSpace.CameraPosition(0f, yOffset);
        transform.rotation = Quaternion.identity;
        _cam.orthographicSize = _originalSize;
    }

    /// <summary>
    /// 动作镜头 (技能 Camera shot 指令)：center 为世界坐标画面中心，zoom 为正交尺寸倍率，roll 为屏幕内旋转 (度)，weight 为与跟随镜头的混合权重
    /// </summary>
    public void SetActionShot(Vector2 center, float zoom, float roll, float weight)
    {
        _actionCenter = center;
        _actionZoom = zoom;
        _actionRoll = roll;
        _actionWeight = weight;
    }

    public void ClearActionShot()
    {
        _actionWeight = 0f;
        _actionZoom = 1f;
        _actionRoll = 0f;
    }

    /// <summary>
    /// 演出镜头 (Timeline)：参数含义同 SetActionShot，叠加在动作镜头之上
    /// </summary>
    public void SetCinematicShot(Vector2 center, float zoom, float roll, float weight)
    {
        _shotCenter = center;
        _shotZoom = zoom;
        _shotRoll = roll;
        _shotWeight = weight;
    }

    /// <summary>
    /// 演出晃动 (Timeline)：本帧位移 / 旋转，以及当前的振幅上限 (用于预留边界余量)
    /// </summary>
    public void SetCinematicShake(Vector2 offset, float roll, float amplitude, float rollAmplitude)
    {
        _cinematicShakeOffset = offset;
        _cinematicShakeRoll = roll;
        _cinematicShakeAmplitude = amplitude;
        _cinematicShakeRollAmplitude = rollAmplitude;
    }

    public void ClearCinematic()
    {
        _shotWeight = 0f;
        _shotZoom = 1f;
        _shotRoll = 0f;
        _cinematicShakeOffset = Vector2.zero;
        _cinematicShakeRoll = 0f;
        _cinematicShakeAmplitude = 0f;
        _cinematicShakeRollAmplitude = 0f;
    }

    /// <summary>
    /// 上下左右晃动：amplitude 世界单位，frequency Hz，duration 秒内振幅线性衰减到 0；rollAmplitude 为旋转抖动幅度 (度)
    /// </summary>
    public int Shake(float amplitude, float frequency, float duration, float rollAmplitude = 0f)
    {
        if ((amplitude <= 0f && rollAmplitude <= 0f) || duration <= 0f) return 0;
        _shaking = true;
        _shakeId = ++_nextEffectId;
        _shakeAmplitude = Mathf.Max(0f, amplitude);
        _shakeFrequency = Mathf.Max(0f, frequency);
        _shakeRollAmplitude = Mathf.Max(0f, rollAmplitude);
        _shakeDuration = duration;
        _shakeTime = 0f;
        return _shakeId;
    }

    /// <summary>
    /// 停止晃动 (只停止编号对应的那次，已被覆盖时忽略)
    /// </summary>
    public void StopShake(int id)
    {
        if (_shaking && id == _shakeId) _shaking = false;
    }

    /// <summary>
    /// 缩放脉冲 (放大缩小震动)：zoom 为脉冲峰值的正交尺寸倍率 (只能推近，> 1 按 1 处理)，
    /// 每次 inSec 推近 -> holdSec 保持 -> outSec 复位，共 count 次，间隔 interval 秒
    /// 新的脉冲覆盖正在进行的脉冲；返回编号供 StopPulse 使用
    /// </summary>
    public int Pulse(float zoom, float inSec, float holdSec, float outSec, int count, float interval, CameraEase ease)
    {
        if (_originalSize <= 0f || count <= 0) return 0;
        _pulsing = true;
        _pulseId = ++_nextEffectId;
        _pulseZoom = Mathf.Clamp(zoom, MinZoom, 1f);
        _pulseIn = Mathf.Max(0f, inSec);
        _pulseHold = Mathf.Max(0f, holdSec);
        _pulseOut = Mathf.Max(0f, outSec);
        _pulseInterval = Mathf.Max(0f, interval);
        _pulseCount = count;
        _pulseEase = ease;
        _pulseTime = 0f;
        return _pulseId;
    }

    public void StopPulse(int id)
    {
        if (_pulsing && id == _pulseId) _pulsing = false;
    }

    /// <summary>
    /// 打击冲击：推近 5% 保持 40ms，重击连续两次
    /// </summary>
    public void ImpactEffect(bool isHeavyHit = false)
    {
        Pulse(ImpactZoom, 0f, ImpactHold, 0f, isHeavyHit ? 2 : 1, ImpactHold, CameraEase.Linear);
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

    private void LateUpdate()
    {
        if (Target == null) return;
        float dt = Time.deltaTime;

        // 1. 追踪角色视图位置 (表现层插值后的位置，避免追踪逻辑位置导致画面相对抖动)，缓动跟随
        _followX = Mathf.Lerp(_followX, DesiredX(), dt * smoothSpeed);

        // 2. 动作镜头按权重与跟随镜头混合，演出镜头再叠加在其上
        float aw = Mathf.Clamp01(_actionWeight);
        Vector2 center = Vector2.Lerp(new Vector2(_followX, yOffset), _actionCenter, aw);
        float zoom = Mathf.Lerp(1f, _actionZoom, aw);
        float roll = _actionRoll * aw;

        float w = Mathf.Clamp01(_shotWeight);
        center = Vector2.Lerp(center, _shotCenter, w);
        zoom = Mathf.Clamp(Mathf.Lerp(zoom, _shotZoom, w), MinZoom, MaxZoomOut);
        roll = Mathf.Lerp(roll, _shotRoll, w);

        // 3. 晃动：演出晃动 + 晃动指令 (振幅随时间衰减)，每轴不超过当前振幅
        Vector2 shake = _cinematicShakeOffset;
        float shakeRoll = _cinematicShakeRoll;
        float shakeAmp = _cinematicShakeAmplitude;
        float shakeRollAmp = _cinematicShakeRollAmplitude;
        UpdateShake(dt, ref shake, ref shakeRoll, ref shakeAmp, ref shakeRollAmp);
        shakeAmp = Mathf.Min(shakeAmp, _originalSize * (1f - MinZoom));
        shake.x = Mathf.Clamp(shake.x, -shakeAmp, shakeAmp);
        shake.y = Mathf.Clamp(shake.y, -shakeAmp, shakeAmp);

        // 4. 边界：box 为视口 (未旋转) 外接框的半高。推近时视口可在正常取景范围内移动；拉远时横向收紧、画面下沿不动。
        //    晃动时预留振幅的余量 (必要时略微推近)，晃到取景边缘也不露出范围外
        float box = _originalSize * zoom;
        if (shakeAmp > 0f) box = Mathf.Min(box, _originalSize - shakeAmp);
        float aspect = _cam.aspect;
        float marginX = (_originalSize - box) * aspect;
        float lo = minX - marginX + shakeAmp;
        float hi = maxX + marginX - shakeAmp;
        float x = lo > hi ? (minX + maxX) * 0.5f : Mathf.Clamp(center.x, lo, hi);
        float y;
        if (box <= _originalSize)
        {
            float marginY = _originalSize - box - shakeAmp;
            y = Mathf.Clamp(center.y, yOffset - marginY, yOffset + marginY);
        }
        else
        {
            y = yOffset + (box - _originalSize);
        }

        // 5. 旋转补偿：按可能出现的最大旋转角缩小正交尺寸，使旋转后的视口完整落在外接框内 (四角不露黑底，抖动时尺寸不跳变)
        float finalRoll = Mathf.Clamp(roll + shakeRoll, -MaxRoll, MaxRoll);
        float r = Mathf.Min(MaxRoll, Mathf.Abs(roll) + shakeRollAmp) * Mathf.Deg2Rad;
        float size = box / (Mathf.Cos(r) + aspect * Mathf.Sin(r));

        // 6. 缩放脉冲 (只推近，始终在外接框内)
        size *= UpdatePulse(dt);

        // 7. 写入相机 (晃动位移最后叠加，余量已在边界中预留)
        transform.position = BattleViewSpace.CameraPosition(x + shake.x, y + shake.y);
        transform.rotation = Quaternion.Euler(0f, 0f, finalRoll);
        _cam.orthographicSize = size;
    }

    private void UpdateShake(float dt, ref Vector2 offset, ref float roll, ref float amplitude, ref float rollAmplitude)
    {
        if (!_shaking) return;
        _shakeTime += dt;
        if (_shakeTime >= _shakeDuration)
        {
            _shaking = false;
            return;
        }

        float decay = 1f - _shakeTime / _shakeDuration;
        float amp = _shakeAmplitude * decay;
        float rollAmp = _shakeRollAmplitude * decay;
        float t = _shakeTime * _shakeFrequency;
        offset += amp * new Vector2(Noise(t, 0.1f), Noise(0.7f, t));
        roll += rollAmp * Noise(t, 0.4f);
        amplitude += amp;
        rollAmplitude += rollAmp;
    }

    /// <summary>
    /// 当前脉冲的正交尺寸倍率 (≤ 1)
    /// </summary>
    private float UpdatePulse(float dt)
    {
        if (!_pulsing) return 1f;
        _pulseTime += dt;

        float period = _pulseIn + _pulseHold + _pulseOut + _pulseInterval;
        int index = period > 0f ? (int)(_pulseTime / period) : _pulseCount;
        if (index >= _pulseCount)
        {
            _pulsing = false;
            return 1f;
        }

        float local = _pulseTime - index * period;
        float v;
        if (local < _pulseIn)
        {
            v = CameraEasing.Evaluate(_pulseEase, local / _pulseIn);
        }
        else if ((local -= _pulseIn) < _pulseHold)
        {
            v = 1f;
        }
        else if ((local -= _pulseHold) < _pulseOut)
        {
            v = 1f - CameraEasing.Evaluate(_pulseEase, local / _pulseOut);
        }
        else
        {
            v = 0f;
        }
        return Mathf.Lerp(1f, _pulseZoom, Mathf.Clamp01(v));
    }

    private static float Noise(float a, float b) => Mathf.Clamp01(Mathf.PerlinNoise(a, b)) * 2f - 1f;
}
