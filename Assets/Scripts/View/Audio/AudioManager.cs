using System.Collections.Generic;
using KiHan.Config;
using KiHan.Logic;
using KiHan.View.Render;
using Managers;
using UnityEngine;

namespace KiHan.View.Audio
{
    /// <summary>
    /// 表现层音频管理：固定数量的 2D AudioSource 轮流播放 SoundCue，另有一条给 Timeline AudioTrack 绑定的 CinematicSource
    /// - 音效 key 优先在角色包内解析 (SoundCue / AudioClip)，其次全局 Resources；加载阶段 Preload，局内只做字典查找
    /// - 播放返回句柄 (> 0)，可提前 Stop；Sound 事件要求 stopOnActionEnd 时随触发者动作结束停止
    /// - 只读逻辑数据，不影响确定性
    /// </summary>
    public class AudioManager : UnitySingleton<AudioManager>
    {
        private const int SourceCount = 16;
        private const int TrackCapacity = 16;

        private struct Channel
        {
            public AudioSource Source;
            public int Handle;
            public float StartTime;
        }

        private struct Tracked
        {
            public int Handle;
            public int EntityId;
            public int ActionSerial;
        }

        private Channel[] _channels;
        private int _nextHandle;

        private readonly Dictionary<CharacterManifest, Dictionary<string, SoundCue>> _packageCues = new Dictionary<CharacterManifest, Dictionary<string, SoundCue>>();
        private readonly Dictionary<string, SoundCue> _globalCues = new Dictionary<string, SoundCue>();

        /// <summary>
        /// FromClip 创建的运行时包装实例 (ClearCaches 时销毁)
        /// </summary>
        private readonly List<SoundCue> _wrappedCues = new List<SoundCue>();

        private readonly Dictionary<SoundCue, float> _lastPlayTime = new Dictionary<SoundCue, float>();
        private readonly Dictionary<int, int> _voiceByEntity = new Dictionary<int, int>();
        private readonly List<Tracked> _tracked = new List<Tracked>(TrackCapacity);

        private bool _conventionsLoaded;

        /// <summary>
        /// 固定约定音效 (未配置时为 null)
        /// </summary>
        public SoundConventions Conventions { get; private set; }

        /// <summary>
        /// 演出 Timeline 的 AudioTrack 输出
        /// </summary>
        public AudioSource CinematicSource { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _channels = new Channel[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                _channels[i].Source = CreateSource("Source" + i);
            }
            CinematicSource = CreateSource("Cinematic");
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>
        /// 加载约定音效 (只加载一次，重复调用直接返回)
        /// </summary>
        public void LoadConventions()
        {
            if (_conventionsLoaded) return;
            _conventionsLoaded = true;
            Conventions = Resources.Load<SoundConventions>(SoundConventions.ResourcePath);
            if (Conventions == null) Debug.LogWarning("[Audio] 缺少 Resources/" + SoundConventions.ResourcePath + "，约定音效静音");
        }

        /// <summary>
        /// 加载阶段解析音效 key：包内 SoundCue -> 包内 AudioClip -> Resources SoundCue -> Resources AudioClip
        /// </summary>
        public void Preload(CharacterManifest manifest, string key, bool optional = false)
        {
            if (string.IsNullOrEmpty(key)) return;

            Dictionary<string, SoundCue> package = null;
            if (manifest != null)
            {
                if (!_packageCues.TryGetValue(manifest, out package))
                {
                    package = new Dictionary<string, SoundCue>();
                    _packageCues[manifest] = package;
                }
                if (package.ContainsKey(key)) return;

                SoundCue cue = manifest.FindResource<SoundCue>(key);
                if (cue == null)
                {
                    AudioClip clip = manifest.FindResource<AudioClip>(key);
                    if (clip != null) cue = Wrap(clip);
                }
                if (cue != null)
                {
                    package[key] = cue;
                    return;
                }
            }

            if (_globalCues.ContainsKey(key)) return;

            SoundCue global = Resources.Load<SoundCue>(key);
            if (global == null)
            {
                AudioClip clip = Resources.Load<AudioClip>(key);
                if (clip != null) global = Wrap(clip);
            }
            if (global != null)
            {
                _globalCues[key] = global;
                return;
            }

            if (!optional) Debug.LogWarning($"[Audio] 音效不存在: {key} (角色 {manifest?.id})");
        }

        private SoundCue Wrap(AudioClip clip)
        {
            SoundCue cue = SoundCue.FromClip(clip);
            _wrappedCues.Add(cue);
            return cue;
        }

        /// <summary>
        /// 查找已预载的音效 (先包内后全局)，没有返回 null
        /// </summary>
        public SoundCue Find(CharacterManifest manifest, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (manifest != null && _packageCues.TryGetValue(manifest, out Dictionary<string, SoundCue> package) && package.TryGetValue(key, out SoundCue cue)) return cue;
            return _globalCues.TryGetValue(key, out SoundCue global) ? global : null;
        }

        /// <summary>
        /// 播放音效，返回句柄 (> 0)；cue 为空、没有音频、处于冷却或没有可用声道时返回 0
        /// ownerEntityId != 0 且类别为 Voice 时，先停掉该实体上一条语音
        /// </summary>
        public int Play(SoundCue cue, int ownerEntityId = 0, bool loop = false)
        {
            if (cue == null || cue.clips == null || cue.clips.Length == 0) return 0;

            float now = Time.unscaledTime;
            if (_lastPlayTime.TryGetValue(cue, out float last) && now - last < cue.cooldown) return 0;

            AudioClip clip = cue.clips[cue.clips.Length == 1 ? 0 : Random.Range(0, cue.clips.Length)];
            if (clip == null) return 0;

            bool voice = cue.category == SoundCategory.Voice && ownerEntityId != 0;
            if (voice && _voiceByEntity.TryGetValue(ownerEntityId, out int previous)) Stop(previous);

            int index = PickChannel();
            if (index < 0) return 0;

            _lastPlayTime[cue] = now;

            int handle = ++_nextHandle;
            if (handle <= 0) handle = _nextHandle = 1;

            ref Channel channel = ref _channels[index];
            AudioSource source = channel.Source;
            source.Stop();
            source.clip = clip;
            source.volume = cue.volume;
            source.pitch = cue.pitch.x >= cue.pitch.y ? cue.pitch.x : Random.Range(cue.pitch.x, cue.pitch.y);
            source.loop = loop;
            source.Play();
            channel.Handle = handle;
            channel.StartTime = now;

            if (voice) _voiceByEntity[ownerEntityId] = handle;
            return handle;
        }

        /// <summary>
        /// 空闲声道优先；没有时抢占最早开始的非循环声道；全部循环时返回 -1
        /// </summary>
        private int PickChannel()
        {
            int oldest = -1;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _channels.Length; i++)
            {
                AudioSource source = _channels[i].Source;
                if (!source.isPlaying) return i;
                if (!source.loop && _channels[i].StartTime < oldestTime)
                {
                    oldest = i;
                    oldestTime = _channels[i].StartTime;
                }
            }
            return oldest;
        }

        private int ChannelOf(int handle)
        {
            if (handle <= 0) return -1;
            for (int i = 0; i < _channels.Length; i++)
            {
                if (_channels[i].Handle == handle) return i;
            }
            return -1;
        }

        /// <summary>
        /// 停止句柄对应的音效 (句柄不存在或已被抢占时忽略)
        /// </summary>
        public void Stop(int handle)
        {
            int index = ChannelOf(handle);
            if (index < 0) return;
            _channels[index].Source.Stop();
            _channels[index].Handle = 0;
        }

        private bool IsActive(int handle)
        {
            int index = ChannelOf(handle);
            return index >= 0 && _channels[index].Source.isPlaying;
        }

        /// <summary>
        /// 动作 Sound 事件：在触发者角色包中解析 key；stopOnActionEnd 时随触发者动作结束停止
        /// </summary>
        public void PlayEvent(in ViewEvent evt)
        {
            int handle = Play(Find(CharacterRegistry.Find(evt.CharacterId), evt.Asset), evt.EntityId, evt.Loop);
            if (handle == 0 || !evt.StopOnActionEnd) return;
            _tracked.Add(new Tracked { Handle = handle, EntityId = evt.EntityId, ActionSerial = evt.ActionSerial });
        }

        /// <summary>
        /// 播放角色包内的约定音效 (如受击语音)
        /// </summary>
        public void PlayCharacter(int characterId, string key, int ownerEntityId)
        {
            Play(Find(CharacterRegistry.Find(characterId), key), ownerEntityId);
        }

        private void Update()
        {
            if (_tracked.Count == 0) return;

            ViewManager views = ViewManager.Instance;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                Tracked t = _tracked[i];
                if (!IsActive(t.Handle))
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                ActorView view = views != null ? views.GetView(t.EntityId) : null;
                CharacterObj character = view != null ? view.Character : null;
                int serial = character != null && character.action != null ? character.action.ActionSerial : 0;
                if (character == null || serial != t.ActionSerial)
                {
                    Stop(t.Handle);
                    _tracked.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 停止全部音效 (含演出音轨)
        /// </summary>
        public void StopAll()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                _channels[i].Source.Stop();
                _channels[i].Handle = 0;
            }
            CinematicSource.Stop();
            _tracked.Clear();
            _voiceByEntity.Clear();
        }

        /// <summary>
        /// 清空预载缓存 (退出战斗时)，销毁 AudioClip 的运行时包装
        /// </summary>
        public void ClearCaches()
        {
            _packageCues.Clear();
            _globalCues.Clear();
            _lastPlayTime.Clear();
            foreach (SoundCue cue in _wrappedCues)
            {
                if (cue != null) Destroy(cue);
            }
            _wrappedCues.Clear();
        }
    }
}
