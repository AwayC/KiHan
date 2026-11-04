using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 演出全屏层：全屏视频 + 全屏遮罩 (ScreenVideoTrack / ScreenFadeTrack 绑定对象)
    /// 画布排序 150：高于 Hud 层 (100)，低于 Normal 层 (200)；不拦截触摸
    /// 视频不出声 (配音走 Timeline AudioTrack)
    /// </summary>
    public class CinematicOverlay : UnitySingleton<CinematicOverlay>
    {
        private const int SortingOrder = 150;

        private RawImage _video;
        private AspectRatioFitter _fitter;
        private Image _fade;
        private VideoPlayer _player;
        private RenderTexture _texture;

        protected override void Awake()
        {
            base.Awake();

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            _video = CreateFullScreen<RawImage>(canvasGo.transform, "Video");
            _video.color = Color.white;
            _fitter = _video.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            _fade = CreateFullScreen<Image>(canvasGo.transform, "Fade");

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.VideoClip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
        }

        private static T CreateFullScreen<T>(Transform parent, string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            T graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            graphic.enabled = false;
            return graphic;
        }

        /// <summary>
        /// 全屏遮罩颜色 (alpha <= 0 隐藏)
        /// </summary>
        public void SetFade(Color c)
        {
            if (c.a <= 0f)
            {
                if (_fade.enabled) _fade.enabled = false;
                return;
            }
            _fade.color = c;
            if (!_fade.enabled) _fade.enabled = true;
        }

        /// <summary>
        /// 预加载视频 (演出开始时调用，减少首帧黑屏)
        /// </summary>
        public void Prepare(VideoClip clip)
        {
            if (clip == null) return;
            if (_player.clip != clip) _player.clip = clip;
            _player.Prepare();
        }

        /// <summary>
        /// 从 time 秒开始全屏播放视频 (按视频宽高比铺满屏幕，超出部分裁掉)
        /// </summary>
        public void PlayVideo(VideoClip clip, double time)
        {
            if (clip == null) return;

            int width = (int)clip.width;
            int height = (int)clip.height;
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                ReleaseTexture();
                _texture = new RenderTexture(width, height, 0) { name = "CinematicVideo" };
                _player.targetTexture = _texture;
                _video.texture = _texture;
            }
            ClearTexture();
            _fitter.aspectRatio = height > 0 ? (float)width / height : 1f;

            if (_player.clip != clip) _player.clip = clip;
            _player.time = time;
            _player.Play();
            _video.enabled = true;
        }

        /// <summary>
        /// 播放进度与演出时间偏差超过 0.1 秒时对齐
        /// </summary>
        public void SyncVideo(VideoClip clip, double time)
        {
            if (_player.clip != clip || !_player.isPlaying) return;
            if (Mathf.Abs((float)(_player.time - time)) > 0.1f) _player.time = time;
        }

        /// <summary>
        /// 停止视频 (只在当前播放的就是 clip 时)
        /// </summary>
        public void StopVideo(VideoClip clip)
        {
            if (_player.clip != clip) return;
            StopVideo();
        }

        private void StopVideo()
        {
            _player.Stop();
            _video.enabled = false;
            ClearTexture();
        }

        /// <summary>
        /// 停止视频并隐藏全部全屏层
        /// </summary>
        public void Clear()
        {
            StopVideo();
            _fade.enabled = false;
        }

        /// <summary>
        /// 清成黑色：避免下次播放首帧前闪出上一段视频的残帧
        /// </summary>
        private void ClearTexture()
        {
            if (_texture == null) return;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;
        }

        private void ReleaseTexture()
        {
            if (_texture == null) return;
            _player.targetTexture = null;
            _video.texture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        protected override void OnDestroy()
        {
            ReleaseTexture();
            base.OnDestroy();
        }
    }
}
