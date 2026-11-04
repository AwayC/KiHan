using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 颜色按权重归一化混合，不透明度按权重累加 (单片段淡入时 alpha 随权重渐变)
    /// </summary>
    public class ScreenFadeMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var overlay = playerData as CinematicOverlay;
            if (overlay == null) return;

            Color c = Color.clear;
            float tw = 0f;
            int count = playable.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;

                ScreenFadeBehaviour fade = ((ScriptPlayable<ScreenFadeBehaviour>)playable.GetInput(i)).GetBehaviour();
                c += w * fade.color;
                tw += w;
            }

            if (tw > 0f)
            {
                c.r /= tw;
                c.g /= tw;
                c.b /= tw;
                overlay.SetFade(c);
            }
            else
            {
                overlay.SetFade(Color.clear);
            }
        }
    }
}
