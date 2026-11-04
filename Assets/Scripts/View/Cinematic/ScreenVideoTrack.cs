using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 全屏视频轨道 (绑定 CinematicOverlay，由 CinematicPlayer 自动绑定)；视频不出声，配音放 AudioTrack
    /// </summary>
    [TrackColor(0.8f, 0.2f, 0.6f)]
    [TrackClipType(typeof(ScreenVideoClip))]
    [TrackBindingType(typeof(CinematicOverlay))]
    public class ScreenVideoTrack : TrackAsset
    {
    }
}
