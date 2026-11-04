using KiHan.Asset;
using UnityEngine;

namespace KiHan.View.Audio
{
    /// <summary>
    /// 特效伴随音效：挂在特效预制体 (或其子节点) 上，特效出池时播放、回收时停止
    /// 走 IPoolable 而不是 OnEnable：对象池预热时实例会短暂激活，OnEnable 会在加载阶段误响
    /// </summary>
    public class FxSound : MonoBehaviour, IPoolable
    {
        [SerializeField] private SoundCue cue;

        [Tooltip("出池后延迟多少秒播放")]
        [SerializeField] private float delay;

        [Tooltip("循环播放直到特效回收")]
        [SerializeField] private bool loop;

        private int _handle;
        private float _timer;
        private bool _pending;

        public void OnSpawn()
        {
            if (delay <= 0f)
            {
                _handle = AudioManager.Instance.Play(cue, 0, loop);
                return;
            }
            _timer = delay;
            _pending = true;
        }

        public void OnRecycle()
        {
            if (_handle != 0)
            {
                AudioManager manager = AudioManager.Instance;
                if (manager != null) manager.Stop(_handle);
            }
            _handle = 0;
            _timer = 0f;
            _pending = false;
        }

        private void Update()
        {
            if (!_pending) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _pending = false;
            _handle = AudioManager.Instance.Play(cue, 0, loop);
        }
    }
}
