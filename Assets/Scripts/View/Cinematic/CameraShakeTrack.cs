using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 演出震屏轨道：位移 + 可选旋转抖动 (绑定 CameraControllor，由 CinematicPlayer 自动绑定)
    /// </summary>
    [TrackColor(1f, 0.5f, 0.2f)]
    [TrackClipType(typeof(CameraShakeClip))]
    [TrackBindingType(typeof(CameraControllor))]
    public class CameraShakeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<CameraShakeMixer>.Create(graph, inputCount);
        }
    }
}
