using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace KiHan.View.Cinematic
{
    [Serializable]
    public class ScreenVideoClip : PlayableAsset, ITimelineClipAsset
    {
        public VideoClip video;

        public ClipCaps clipCaps => ClipCaps.None;

        public override double duration => video != null ? video.length : base.duration;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ScreenVideoBehaviour>.Create(graph);
            playable.GetBehaviour().video = video;
            return playable;
        }
    }
}
