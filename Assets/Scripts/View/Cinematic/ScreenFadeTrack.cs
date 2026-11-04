using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 全屏遮罩轨道：片段淡入淡出即遮罩渐变 (绑定 CinematicOverlay，由 CinematicPlayer 自动绑定)
    /// </summary>
    [TrackColor(0.3f, 0.3f, 0.3f)]
    [TrackClipType(typeof(ScreenFadeClip))]
    [TrackBindingType(typeof(CinematicOverlay))]
    public class ScreenFadeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<ScreenFadeMixer>.Create(graph, inputCount);
        }
    }
}
