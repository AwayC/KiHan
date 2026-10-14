using System;
using KiHan.Asset;
using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Render
{
    /// <summary>
    /// 3D 纵深特效表现组件
    /// 核心职责：
    /// 1. 空间定位：独立特效放在 EffectData.WorldPos (已由 BattleViewSpace 映射)；跟随特效挂到 ActorView.Body
    /// 2. 全类型动画播放：无缝支持 3D ParticleSystem、2D 序列帧 Animator、Timeline PlayableDirector
    /// 3. 生命周期与对象池契约 (IPoolable)：播放结束全自动回收至 ObjectPoolManager；
    ///    播放检测在 Update 中按固定间隔轮询 (不使用协程与 WaitForSeconds，0 GC)
    /// </summary>
    public class EffectView : MonoBehaviour, IPoolable
    {
        /// <summary>
        /// 播放状态检测间隔 (秒)
        /// </summary>
        private const float CheckInterval = 0.1f;

        /// <summary>
        /// 自动检测模式下的防泄漏超时 (秒)
        /// </summary>
        private const float MaxLifetime = 5f;

        /// <summary>
        /// 开始播放后至少保留的时间 (秒)，避免首次检测时动画状态尚未刷新被误判为结束
        /// </summary>
        private const float MinLifetime = 0.15f;

        public int EffectId { get; private set; }
        public string PoolKey { get; set; }

        /// <summary>
        /// 回收时回调 (由 EffectManager 设置一次，用于移除活跃句柄)
        /// </summary>
        public Action<EffectView> Recycled { get; set; }

        [Header("自动回收时间保底 (<=0 则自动检测粒子/动画长度)")]
        [SerializeField] private float fallbackDuration = 0f;

        private Animator _animator;
        private PlayableDirector _director;
        private ParticleSystem[] _particles;
        private SpriteRenderer[] _spriteRenderers;

        private bool _isRecycled = true;
        private bool _loop;
        private float _elapsed;
        private float _nextCheck;

        private void Awake()
        {
            ScanComponents();
        }

        private void ScanComponents()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_director == null) _director = GetComponentInChildren<PlayableDirector>(true);
            if (_particles == null) _particles = GetComponentsInChildren<ParticleSystem>(true);
            if (_spriteRenderers == null) _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        #region IPoolable 生命周期

        public void OnSpawn()
        {
            _isRecycled = false;
            ScanComponents();

            // 恢复所有渲染器可见性
            if (_spriteRenderers != null)
            {
                for (int i = 0; i < _spriteRenderers.Length; i++)
                {
                    if (_spriteRenderers[i] != null) _spriteRenderers[i].enabled = true;
                }
            }

            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i] != null)
                    {
                        _particles[i].Clear();
                    }
                }
            }
        }

        public void OnRecycle()
        {
            _isRecycled = true;

            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i] != null)
                    {
                        _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            Recycled?.Invoke(this);
        }

        #endregion

        /// <summary>
        /// 初始化并触发特效播放
        /// </summary>
        /// <param name="loop">循环播放：不做自动回收，必须由外部 (EffectManager.StopEffect) 停止</param>
        public void Init(int id, EffectData data, bool loop = false)
        {
            EffectId = id;
            _isRecycled = false;
            _loop = loop;
            _elapsed = 0f;
            _nextCheck = CheckInterval;

            // 1. 面向翻转
            Vector3 scale = transform.localScale;
            scale.x = data.IsFacingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;

            Vector3 offset = new Vector3(
                (data.IsFacingLeft ? -data.Offset.x : data.Offset.x) * BattleViewSpace.PixelToWorld,
                data.Offset.y * BattleViewSpace.PixelToWorld,
                0f);

            // 2. 空间定位：跟随角色 vs 独立世界坐标 (均不旋转)
            if (data.BindView != null)
            {
                transform.SetParent(data.BindView.Body, false);
                transform.localPosition = offset;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.SetParent(null);
                transform.SetPositionAndRotation(data.WorldPos + offset, Quaternion.identity);
            }

            // 3. 触发全类型动画播放
            PlayAllAnimationDrivers();
        }

        private void PlayAllAnimationDrivers()
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play(0, -1, 0f);
                _animator.Update(0f);
            }

            if (_director != null && _director.playableAsset != null)
            {
                _director.time = 0;
                _director.Evaluate();
                _director.Play();
            }

            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i] != null) _particles[i].Play();
                }
            }
        }

        private void Update()
        {
            if (_isRecycled || _loop) return;

            _elapsed += Time.deltaTime;

            // 优先根据配置的保底时间
            if (fallbackDuration > 0f)
            {
                if (_elapsed >= fallbackDuration) RecycleSelf();
                return;
            }

            if (_elapsed < _nextCheck) return;
            _nextCheck += CheckInterval;

            if (_elapsed >= MaxLifetime || (_elapsed > MinLifetime && !IsStillPlaying()))
            {
                RecycleSelf();
            }
        }

        /// <summary>
        /// 自动检测各组件的播放状态
        /// </summary>
        private bool IsStillPlaying()
        {
            // 3D 粒子系统
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i] != null && _particles[i].IsAlive(true)) return true;
                }
            }

            // Animator 动画完成度
            if (_animator != null && _animator.runtimeAnimatorController != null &&
                _animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            {
                return true;
            }

            // PlayableDirector Timeline
            return _director != null && _director.playableAsset != null && _director.time < _director.duration;
        }

        /// <summary>
        /// 回收自身回对象池
        /// </summary>
        public void RecycleSelf()
        {
            if (_isRecycled) return;

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Recycle(gameObject);
            }

            // 未经对象池登记 (Recycle 直接销毁、不回调 OnRecycle) 时补发回收回调
            if (!_isRecycled)
            {
                _isRecycled = true;
                Recycled?.Invoke(this);
            }
        }
    }
}
