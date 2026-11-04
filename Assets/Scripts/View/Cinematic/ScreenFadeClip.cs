using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace KiHan.View.Cinematic
{
    [Serializable]
    public class ScreenFadeClip : PlayableAsset, ITimelineClipAsset
    {
        public ScreenFadeBehaviour template = new ScreenFadeBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<ScreenFadeBehaviour>.Create(graph, template);
        }
    }
}
