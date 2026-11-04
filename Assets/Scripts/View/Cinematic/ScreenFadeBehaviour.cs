using System;
using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    [Serializable]
    public class ScreenFadeBehaviour : PlayableBehaviour
    {
        [Tooltip("遮罩颜色 (alpha 为满权重时的不透明度)")]
        public Color color = new Color(0f, 0f, 0f, 1f);
    }
}
