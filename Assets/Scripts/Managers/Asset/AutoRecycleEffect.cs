using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 特效自动回收组件
    /// 可根据生命周期时长 (duration) 或 ParticleSystem 播放完毕状态自动回收至对象池
    /// </summary>
    public class AutoRecycleEffect : MonoBehaviour, IPoolable
    {
        [Tooltip("固定生命时长 (秒)，<= 0 则自动检测 ParticleSystem 播放状态")]
        public float duration = 0f;

        private float _timer = 0f;
        private ParticleSystem[] _particles;

        private void Awake()
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void OnSpawn()
        {
            _timer = 0f;
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    _particles[i].Clear();
                    _particles[i].Play();
                }
            }
        }

        public void OnRecycle()
        {
            _timer = 0f;
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (duration > 0f)
            {
                if (_timer >= duration)
                {
                    RecycleSelf();
                }
            }
            else if (_particles != null && _particles.Length > 0)
            {
                bool anyAlive = false;
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i].IsAlive(true))
                    {
                        anyAlive = true;
                        break;
                    }
                }

                if (!anyAlive && _timer > 0.1f)
                {
                    RecycleSelf();
                }
            }
        }

        private void RecycleSelf()
        {
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
