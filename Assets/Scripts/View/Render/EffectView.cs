using System.Collections;
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
    /// 3. 生命周期与对象池契约 (IPoolable)：播放结束全自动回收至 ObjectPoolManager，0 GC
    /// </summary>
    public class EffectView : MonoBehaviour, IPoolable
    {
        public int EffectId { get; private set; }
        public string PoolKey { get; set; }

        [Header("自动回收时间保底 (<=0 则自动检测粒子/动画长度)")]
        [SerializeField] private float fallbackDuration = 0f;

        private Animator _animator;
        private PlayableDirector _director;
        private ParticleSystem[] _particles;
        private SpriteRenderer[] _spriteRenderers;

        private bool _isRecycled = false;
        private Coroutine _autoRecycleCoroutine;

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
            if (_autoRecycleCoroutine != null)
            {
                StopCoroutine(_autoRecycleCoroutine);
                _autoRecycleCoroutine = null;
            }

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
        }

        #endregion

        /// <summary>
        /// 初始化并触发特效播放
        /// </summary>
        public void Init(int id, EffectData data)
        {
            EffectId = id;

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

            // 4. 启动自动回收检测
            if (gameObject.activeInHierarchy)
            {
                if (_autoRecycleCoroutine != null) StopCoroutine(_autoRecycleCoroutine);
                _autoRecycleCoroutine = StartCoroutine(AutoRecycleRoutine());
            }
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

        private IEnumerator AutoRecycleRoutine()
        {
            // 优先根据配置的保底时间
            if (fallbackDuration > 0f)
            {
                yield return new WaitForSeconds(fallbackDuration);
                RecycleSelf();
                yield break;
            }

            // 自动检测各组件的播放状态
            float elapsedTime = 0f;
            const float MAX_TIMEOUT = 5.0f; // 5 秒防泄漏超时

            while (elapsedTime < MAX_TIMEOUT)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                bool isStillPlaying = false;

                // 检测 3D 粒子系统
                if (_particles != null && _particles.Length > 0)
                {
                    for (int i = 0; i < _particles.Length; i++)
                    {
                        if (_particles[i] != null && _particles[i].IsAlive(true))
                        {
                            isStillPlaying = true;
                            break;
                        }
                    }
                }

                // 检测 Animator 动画完成度
                if (!isStillPlaying && _animator != null && _animator.runtimeAnimatorController != null)
                {
                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.normalizedTime < 1.0f)
                    {
                        isStillPlaying = true;
                    }
                }

                // 检测 PlayableDirector Timeline
                if (!isStillPlaying && _director != null && _director.playableAsset != null)
                {
                    if (_director.time < _director.duration)
                    {
                        isStillPlaying = true;
                    }
                }

                if (!isStillPlaying && elapsedTime > 0.15f)
                {
                    break;
                }
            }

            RecycleSelf();
        }

        /// <summary>
        /// 回收自身回对象池
        /// </summary>
        public void RecycleSelf()
        {
            if (_isRecycled) return;
            _isRecycled = true;

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Recycle(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
            }
        }
    }
}
