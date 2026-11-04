using System;
using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    [Serializable]
    public class CameraShakeBehaviour : PlayableBehaviour
    {
        [Tooltip("位移振幅 (世界单位)")]
        public float amplitude = 0.1f;

        [Tooltip("抖动频率 (Hz)")]
        public float frequency = 20f;

        [Tooltip("旋转振幅 (度)")]
        public float rollAmplitude = 0f;
    }
}
