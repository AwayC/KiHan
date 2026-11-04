using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Video;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 片段开始时全屏播放视频，逐帧与片段时间对齐，片段结束时停止
    /// Play / Pause 回调拿不到绑定对象，直接使用 CinematicOverlay.Instance (只在运行时)
    /// </summary>
    public class ScreenVideoBehaviour : PlayableBehaviour
    {
        public VideoClip video;

        /// <summary>
        /// 本片段确实开始过播放 (图启动时未激活片段也会收到 Pause，不能误停预热中的视频)
        /// </summary>
        private bool _playing;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (video == null || !Application.isPlaying) return;
            CinematicOverlay.Instance.PlayVideo(video, playable.GetTime());
            _playing = true;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!_playing) return;
            var overlay = playerData as CinematicOverlay;
            if (overlay != null) overlay.SyncVideo(video, playable.GetTime());
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!_playing) return;
            _playing = false;
            CinematicOverlay overlay = CinematicOverlay.Instance;
            if (overlay != null) overlay.StopVideo(video);
        }
    }
}
