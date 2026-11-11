using System.Collections.Generic;
using KiHan.Config;
using KiHan.Logic;
using KiHan.View.Audio;
using KiHan.View.Render;
using Managers;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 大招演出播放器：Cinematic 事件引用的预制体 (根节点 PlayableDirector + Timeline) 在加载阶段实例化待用，
    /// 触发时放到施法锚点 (朝左时镜像)、自动绑定镜头 / 全屏层 / 演出音轨后播放
    /// - 按观看者过滤 (Self / Others / All)，同时只播一个演出 (后来者覆盖)
    /// - 演出时间对齐逻辑帧：卡顿或联机等帧时暂停，落后过多时跳转，保证与角色动作同步
    /// - 只读逻辑数据，不影响确定性
    /// </summary>
    public class CinematicPlayer : UnitySingleton<CinematicPlayer>
    {
        private readonly Dictionary<CharacterManifest, Dictionary<string, PlayableDirector>> _package = new Dictionary<CharacterManifest, Dictionary<string, PlayableDirector>>();
        private readonly Dictionary<string, PlayableDirector> _global = new Dictionary<string, PlayableDirector>();
        private readonly CinematicContext _context = new CinematicContext();

        /// <summary>
        /// 实例化时的临时父节点 (未激活)：避免预制体实例化时 PlayableDirector 立即 Awake 播放
        /// </summary>
        private Transform _staging;

        private int _tick;

        /// <summary>
        /// 正在播放的演出 (没有时为 null)
        /// </summary>
        public CinematicContext Current { get; private set; }

        public bool IsPlaying => Current != null;

        protected override void Awake()
        {
            base.Awake();
            var staging = new GameObject("Staging");
            staging.SetActive(false);
            _staging = staging.transform;
            _staging.SetParent(transform, false);
        }

        /// <summary>
        /// 加载阶段实例化演出预制体：包内 prefab 优先，其次全局资源
        /// </summary>
        public void Preload(CharacterManifest manifest, string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            Dictionary<string, PlayableDirector> cache = _global;
            GameObject prefab = null;
            if (manifest != null)
            {
                if (!_package.TryGetValue(manifest, out Dictionary<string, PlayableDirector> package))
                {
                    package = new Dictionary<string, PlayableDirector>();
                    _package[manifest] = package;
                }
                if (package.ContainsKey(key)) return;

                prefab = manifest.FindResource<GameObject>(key);
                if (prefab != null) cache = package;
            }

            if (prefab == null)
            {
                if (_global.ContainsKey(key)) return;
                prefab = KiHan.Asset.AssetManager.Instance.TryLoad<GameObject>(key);
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[Cinematic] 演出资源不存在: {key} (角色 {manifest?.id})");
                return;
            }

            GameObject instance = Instantiate(prefab, _staging);
            instance.SetActive(false);
            PlayableDirector director = instance.GetComponent<PlayableDirector>();
            if (director == null || !(director.playableAsset is TimelineAsset))
            {
                Debug.LogWarning($"[Cinematic] 演出预制体缺少 PlayableDirector 或 Timeline: {key}");
                Destroy(instance);
                return;
            }

            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            instance.transform.SetParent(transform, false);
            cache[key] = director;
        }

        private PlayableDirector Find(CharacterManifest manifest, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (manifest != null && _package.TryGetValue(manifest, out Dictionary<string, PlayableDirector> package) && package.TryGetValue(key, out PlayableDirector director)) return director;
            return _global.TryGetValue(key, out PlayableDirector global) ? global : null;
        }

        /// <summary>
        /// 每个逻辑帧调用 (视图快照推进之后、本帧事件之前)：推进演出时钟，要求随动作结束的演出在施法者换动作时停止
        /// </summary>
        public void OnLogicTick()
        {
            _tick++;

            CinematicContext ctx = Current;
            if (ctx == null || !ctx.StopOnActionEnd) return;

            ActorView view = ViewManager.Instance.GetView(ctx.EntityId);
            CharacterObj character = view != null ? view.Character : null;
            int serial = character != null && character.action != null ? character.action.ActionSerial : 0;
            if (character == null || serial != ctx.ActionSerial) Stop();
        }

        /// <summary>
        /// 观看者过滤：Self 只有释放者主人的本机，Others 只有其他玩家，All 所有人 (Cinematic 与 Camera 事件共用)
        /// </summary>
        public static bool IsVisible(CinematicAudience audience, int ownerSlot, int localSlot)
        {
            switch (audience)
            {
                case CinematicAudience.Self: return ownerSlot == localSlot;
                case CinematicAudience.Others: return ownerSlot != localSlot;
                default: return true;
            }
        }

        /// <summary>
        /// Cinematic 事件：观看者不含本机时忽略；已有演出时先停止
        /// </summary>
        public void Play(in ViewEvent evt, int localSlot)
        {
            if (!IsVisible(evt.Audience, evt.OwnerSlot, localSlot)) return;

            // 找不到时加载阶段已报过警告
            PlayableDirector director = Find(CharacterRegistry.Find(evt.CharacterId), evt.Asset);
            if (director == null) return;

            if (Current != null) Stop();

            Vector3 castPoint = BattleViewSpace.ToWorld(evt.Position);
            Transform root = director.transform;
            root.position = castPoint;
            root.localScale = new Vector3(evt.FacingLeft ? -1f : 1f, 1f, 1f);
            director.gameObject.SetActive(true);
            Bind(director);

            _context.Director = director;
            _context.EntityId = evt.EntityId;
            _context.ActionSerial = evt.ActionSerial;
            _context.StopOnActionEnd = evt.StopOnActionEnd;
            _context.FacingLeft = evt.FacingLeft;
            _context.CastPoint = castPoint;
            _context.Caster = ViewManager.Instance.GetView(evt.EntityId);
            _context.StartTick = _tick;
            Current = _context;

            director.time = 0;
            director.Play();
        }

        /// <summary>
        /// 自定义轨道绑定场景中的单例；AudioTrack 输出到 AudioManager.CinematicSource
        /// </summary>
        private static void Bind(PlayableDirector director)
        {
            var timeline = (TimelineAsset)director.playableAsset;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                switch (track)
                {
                    case CameraShotTrack _:
                    case CameraShakeTrack _:
                        director.SetGenericBinding(track, CameraControllor.Instance);
                        break;
                    case ScreenFadeTrack _:
                        director.SetGenericBinding(track, CinematicOverlay.Instance);
                        break;
                    case ScreenVideoTrack _:
                        director.SetGenericBinding(track, CinematicOverlay.Instance);
                        foreach (TimelineClip clip in track.GetClips())
                        {
                            if (clip.asset is ScreenVideoClip video && video.video != null) CinematicOverlay.Instance.Prepare(video.video);
                        }
                        break;
                    case AudioTrack _:
                        director.SetGenericBinding(track, AudioManager.Instance.CinematicSource);
                        break;
                }
            }
        }

        /// <summary>
        /// 演出时间对齐逻辑帧 (与 ActorView 一样落后一帧插值)：超前一帧以上暂停等待，落后两帧以上直接跳转
        /// </summary>
        private void Update()
        {
            CinematicContext ctx = Current;
            if (ctx == null) return;

            PlayableDirector director = ctx.Director;
            if (director.state != PlayState.Playing || director.time >= director.duration)
            {
                Stop();
                return;
            }

            const float tick = GameConfig.LOGIC_TICK_TIME;
            float alpha = LockstepManager.Instance.TickAlpha;
            double target = Mathf.Max(0f, _tick - ctx.StartTick - 1 + alpha) * tick;

            PlayableGraph graph = director.playableGraph;
            if (graph.IsValid() && graph.GetRootPlayableCount() > 0)
            {
                graph.GetRootPlayable(0).SetSpeed(director.time > target + tick ? 0d : 1d);
            }

            if (director.time < target - 2 * tick) director.time = target;
        }

        /// <summary>
        /// 停止当前演出，复位镜头、全屏层与演出音轨
        /// </summary>
        public void Stop()
        {
            CinematicContext ctx = Current;
            if (ctx == null) return;
            Current = null;

            if (ctx.Director != null)
            {
                ctx.Director.Stop();
                ctx.Director.gameObject.SetActive(false);
            }
            ctx.Director = null;
            ctx.Caster = null;

            CameraControllor cam = CameraControllor.Instance;
            if (cam != null) cam.ClearCinematic();
            CinematicOverlay overlay = CinematicOverlay.Instance;
            if (overlay != null) overlay.Clear();
            AudioManager audio = AudioManager.Instance;
            if (audio != null) audio.CinematicSource.Stop();
        }

        /// <summary>
        /// 退出战斗：停止演出并销毁全部预载实例
        /// </summary>
        public void Clear()
        {
            Stop();
            foreach (Dictionary<string, PlayableDirector> package in _package.Values)
            {
                DestroyAll(package);
            }
            _package.Clear();
            DestroyAll(_global);
            _tick = 0;
        }

        private static void DestroyAll(Dictionary<string, PlayableDirector> cache)
        {
            foreach (PlayableDirector director in cache.Values)
            {
                if (director != null) Destroy(director.gameObject);
            }
            cache.Clear();
        }
    }
}
