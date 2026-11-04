using KiHan.View.Render;
using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 正在播放的演出的上下文 (CinematicPlayer.Current)，自定义轨道据此取施法者位置与朝向
    /// </summary>
    public class CinematicContext
    {
        public PlayableDirector Director;
        public int EntityId;
        public int ActionSerial;
        public bool StopOnActionEnd;
        public bool FacingLeft;

        /// <summary>
        /// 触发事件时锚点的世界坐标
        /// </summary>
        public Vector2 CastPoint;

        /// <summary>
        /// 施法者视图 (可能为 null)
        /// </summary>
        public ActorView Caster;

        /// <summary>
        /// 演出开始时的逻辑帧计数 (CinematicPlayer 内部计数)
        /// </summary>
        public int StartTick;
    }
}
