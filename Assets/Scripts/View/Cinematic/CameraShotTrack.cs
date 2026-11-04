using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 演出镜头轨道：拉远 / 推近、平移、屏幕内旋转 (绑定 CameraControllor，由 CinematicPlayer 自动绑定)
    /// </summary>
    [TrackColor(0.2f, 0.6f, 1f)]
    [TrackClipType(typeof(CameraShotClip))]
    [TrackBindingType(typeof(CameraControllor))]
    public class CameraShotTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<CameraShotMixer>.Create(graph, inputCount);
        }
    }
}
