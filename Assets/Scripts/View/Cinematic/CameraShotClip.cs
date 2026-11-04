using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    [Serializable]
    public class CameraShotClip : PlayableAsset, ITimelineClipAsset
    {
        public CameraShotBehaviour template = new CameraShotBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<CameraShotBehaviour>.Create(graph, template);
        }
    }
}
