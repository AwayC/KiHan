using System;
using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 镜头对准的基准点
    /// </summary>
    public enum CameraShotTarget
    {
        /// <summary>触发 Cinematic 事件时的锚点世界坐标</summary>
        CastPoint,

        /// <summary>施法者视图的实时位置</summary>
        Caster,
    }

    [Serializable]
    public class CameraShotBehaviour : PlayableBehaviour
    {
        public CameraShotTarget target = CameraShotTarget.CastPoint;

        [Tooltip("相对基准点的偏移 (世界单位)，施法者朝左时 x 取反")]
        public Vector2 offset;

        [Tooltip("正交尺寸倍率：< 1 推近，> 1 拉远 (CameraControllor.MinZoom ~ MaxZoomOut)")]
        public float zoom = 1f;

        [Tooltip("屏幕内旋转 (度，逆时针为正)，施法者朝左时取反")]
        public float roll;
    }
}
