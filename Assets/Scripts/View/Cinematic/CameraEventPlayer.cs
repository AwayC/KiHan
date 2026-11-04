using KiHan.Logic;
using KiHan.Math;
using KiHan.View.Render;
using Managers;
using TrueSync;
using UnityEngine;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 动作 Camera 事件播放器：把 impact / shot / shake / pulse / reset 指令转换为 CameraControllor 的镜头层
    /// - shot：从当前镜头状态缓动到事件点 (anchor + offset 或 worldPos，bind 时跟随施法者)，保持后复位；
    ///   未配置 holdSec 时保持到施法者本动作结束。施法者被打 / 被抓 (进入受击状态)、视图消失、动作结束 (按配置) 时提前复位，
    ///   用于抓取等夸张镜头被第三方打断的情况
    /// - shake / pulse：交给相机执行，stopOnActionEnd 时随施法者动作结束停止
    /// 全部效果受相机的取景边界限制；按 audience 过滤观看者；只读逻辑数据
    /// 在角色视图之后、相机之前更新 (跟随点读取本帧插值后的角色位置)
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CameraEventPlayer : UnitySingleton<CameraEventPlayer>
    {
        private enum Phase { None, In, Hold, Out }

        private struct Tracked
        {
            public int Id;
            public int EntityId;
            public int ActionSerial;
        }

        // --- shot ---
        private Phase _phase;
        private float _time;
        private int _entityId;
        private int _actionSerial;
        private int _ownerSlot;
        private HurtState _hurtAtStart;
        private bool _endOnActionEnd;
        private bool _holdUntilActionEnd;

        private bool _follow;
        private Vector2 _point;
        private Vector2 _followDelta;
        private bool _castFacingLeft;

        private float _zoom = 1f;
        private float _roll;
        private float _inSec;
        private float _holdSec;
        private float _outSec;
        private CameraEase _ease;

        private Vector2 _fromCenter;
        private float _fromZoom = 1f;
        private float _fromRoll;
        private float _fromWeight;

        private Vector2 _center;
        private float _currentZoom = 1f;
        private float _currentRoll;
        private float _weight;

        // --- shake / pulse (stopOnActionEnd 时跟踪) ---
        private Tracked _shake;
        private Tracked _pulse;

        /// <summary>
        /// shot 是否生效中 (含复位过程)
        /// </summary>
        public bool IsShotActive => _phase != Phase.None;

        public void Play(in ViewEvent evt, int localSlot)
        {
            if (!CinematicPlayer.IsVisible(evt.Audience, evt.OwnerSlot, localSlot)) return;

            CameraControllor cam = CameraControllor.Instance;
            CameraEventInfo p = evt.Camera;
            switch (evt.Asset)
            {
                case CameraEffects.Shot:
                    StartShot(evt);
                    break;
                case CameraEffects.Shake:
                    int shakeId = cam.Shake(p.amplitude.AsFloat(), p.frequency.AsFloat(), p.duration.AsFloat(), Mathf.Abs(p.roll.AsFloat()));
                    _shake = evt.StopOnActionEnd && shakeId != 0 ? new Tracked { Id = shakeId, EntityId = evt.EntityId, ActionSerial = evt.ActionSerial } : default;
                    break;
                case CameraEffects.Pulse:
                    int pulseId = cam.Pulse(p.zoom.AsFloat(), p.inSec.AsFloat(), p.holdSec.AsFloat(), p.outSec.AsFloat(), p.count, p.interval.AsFloat(), p.ease);
                    _pulse = evt.StopOnActionEnd && pulseId != 0 ? new Tracked { Id = pulseId, EntityId = evt.EntityId, ActionSerial = evt.ActionSerial } : default;
                    break;
                case CameraEffects.Reset:
                    if (_phase != Phase.None && _ownerSlot == evt.OwnerSlot) Release(p.outSec.AsFloat(), p.ease);
                    break;
                default:
                    cam.ImpactEffect(evt.Asset == CameraEffects.ImpactHeavy);
                    break;
            }
        }

        private void StartShot(in ViewEvent evt)
        {
            ActorView caster = ViewManager.Instance.GetView(evt.EntityId);
            CharacterObj character = caster != null ? caster.Character : null;
            CameraEventInfo p = evt.Camera;

            _point = BattleViewSpace.ToWorld(evt.Position);
            _follow = evt.Bind && character != null;
            if (_follow)
            {
                FPVector3 d = evt.Position - character.motion.Position;
                _followDelta = new Vector2(d.x.AsFloat(), (d.y + d.z).AsFloat());
                _castFacingLeft = evt.FacingLeft;
            }

            float sign = evt.FacingLeft ? -1f : 1f;
            _zoom = p.zoom.AsFloat();
            _roll = p.roll.AsFloat() * sign;
            _inSec = Mathf.Max(0f, p.inSec.AsFloat());
            _holdSec = Mathf.Max(0f, p.holdSec.AsFloat());
            _outSec = Mathf.Max(0f, p.outSec.AsFloat());
            _ease = p.ease;
            _holdUntilActionEnd = p.holdUntilActionEnd;
            _endOnActionEnd = p.holdUntilActionEnd || evt.StopOnActionEnd;

            _entityId = evt.EntityId;
            _actionSerial = evt.ActionSerial;
            _ownerSlot = evt.OwnerSlot;
            _hurtAtStart = character != null ? character.Hurt : HurtState.None;

            // 从当前镜头状态出发 (上一个 shot 未结束时平滑衔接，不跳变)
            if (_phase == Phase.None || _weight <= 0f)
            {
                _fromCenter = TargetPoint();
                _fromZoom = 1f;
                _fromRoll = 0f;
                _fromWeight = 0f;
            }
            else
            {
                _fromCenter = _center;
                _fromZoom = _currentZoom;
                _fromRoll = _currentRoll;
                _fromWeight = _weight;
            }

            _phase = Phase.In;
            _time = 0f;
        }

        /// <summary>
        /// 开始复位 (outSec 秒内按 ease 回到跟随镜头)
        /// </summary>
        private void Release(float outSec, CameraEase ease)
        {
            if (_phase == Phase.None || _phase == Phase.Out) return;
            _fromCenter = _center;
            _fromZoom = _currentZoom;
            _fromRoll = _currentRoll;
            _fromWeight = _weight;
            _outSec = Mathf.Max(0f, outSec);
            _ease = ease;
            _phase = Phase.Out;
            _time = 0f;
        }

        private Vector2 TargetPoint()
        {
            if (!_follow) return _point;
            ActorView caster = ViewManager.Instance.GetView(_entityId);
            if (caster == null || caster.Character == null) return _point;

            // 跟随施法者 (含高度)；施法者转身时目标点随之镜像
            Vector2 delta = _followDelta;
            if (caster.Character.FacingLeft != _castFacingLeft) delta.x = -delta.x;
            Vector3 body = caster.Body.position;
            return new Vector2(body.x, body.y) + delta;
        }

        /// <summary>
        /// 每个逻辑帧调用 (视图快照推进之后、本帧事件之前)：施法者被打断 / 动作结束时复位镜头、停止晃动与脉冲
        /// </summary>
        public void OnLogicTick()
        {
            if (_phase == Phase.In || _phase == Phase.Hold)
            {
                CharacterObj caster = CharacterOf(_entityId);
                bool interrupted = caster == null
                                   || (caster.Hurt != HurtState.None && caster.Hurt != _hurtAtStart)
                                   || (_endOnActionEnd && SerialOf(caster) != _actionSerial);
                if (interrupted) Release(_outSec, _ease);
            }

            CameraControllor cam = CameraControllor.Instance;
            if (_shake.Id != 0 && ActionEnded(_shake))
            {
                cam.StopShake(_shake.Id);
                _shake = default;
            }
            if (_pulse.Id != 0 && ActionEnded(_pulse))
            {
                cam.StopPulse(_pulse.Id);
                _pulse = default;
            }
        }

        private static CharacterObj CharacterOf(int entityId)
        {
            ActorView view = ViewManager.Instance.GetView(entityId);
            return view != null ? view.Character : null;
        }

        private static int SerialOf(CharacterObj character) => character.action != null ? character.action.ActionSerial : 0;

        private static bool ActionEnded(in Tracked tracked)
        {
            CharacterObj character = CharacterOf(tracked.EntityId);
            return character == null || SerialOf(character) != tracked.ActionSerial;
        }

        private void LateUpdate()
        {
            if (_phase == Phase.None) return;
            _time += Time.deltaTime;

            switch (_phase)
            {
                case Phase.In:
                {
                    float t = _inSec > 0f ? _time / _inSec : 1f;
                    float e = CameraEasing.Evaluate(_ease, t);
                    Apply(Vector2.LerpUnclamped(_fromCenter, TargetPoint(), e),
                        Mathf.LerpUnclamped(_fromZoom, _zoom, e),
                        Mathf.LerpUnclamped(_fromRoll, _roll, e),
                        Mathf.Lerp(_fromWeight, 1f, e));
                    if (t >= 1f)
                    {
                        _phase = Phase.Hold;
                        _time = 0f;
                    }
                    break;
                }
                case Phase.Hold:
                    Apply(TargetPoint(), _zoom, _roll, 1f);
                    if (!_holdUntilActionEnd && _time >= _holdSec) Release(_outSec, _ease);
                    break;
                case Phase.Out:
                {
                    float t = _outSec > 0f ? _time / _outSec : 1f;
                    if (t >= 1f)
                    {
                        Stop();
                        return;
                    }
                    float e = CameraEasing.Evaluate(_ease, t);
                    Apply(_fromCenter, _fromZoom, _fromRoll, Mathf.LerpUnclamped(_fromWeight, 0f, e));
                    break;
                }
            }
        }

        private void Apply(Vector2 center, float zoom, float roll, float weight)
        {
            _center = center;
            _currentZoom = zoom;
            _currentRoll = roll;
            _weight = Mathf.Clamp01(weight);
            CameraControllor.Instance.SetActionShot(_center, _currentZoom, _currentRoll, _weight);
        }

        private void Stop()
        {
            _phase = Phase.None;
            _weight = 0f;
            _currentZoom = 1f;
            _currentRoll = 0f;
            CameraControllor cam = CameraControllor.Instance;
            if (cam != null) cam.ClearActionShot();
        }

        /// <summary>
        /// 退出战斗：立即结束全部动作镜头效果
        /// </summary>
        public void Clear()
        {
            Stop();
            CameraControllor cam = CameraControllor.Instance;
            if (cam != null)
            {
                if (_shake.Id != 0) cam.StopShake(_shake.Id);
                if (_pulse.Id != 0) cam.StopPulse(_pulse.Id);
            }
            _shake = default;
            _pulse = default;
        }
    }
}
