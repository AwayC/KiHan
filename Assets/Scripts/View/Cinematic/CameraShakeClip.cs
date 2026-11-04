using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    [Serializable]
    public class CameraShakeClip : PlayableAsset, ITimelineClipAsset
    {
        public CameraShakeBehaviour template = new CameraShakeBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<CameraShakeBehaviour>.Create(graph, template);
        }
    }
}
