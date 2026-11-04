using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 各震屏片段按权重叠加 Perlin 噪声 (以片段本地时间采样，演出暂停时画面同步静止)
    /// </summary>
    public class CameraShakeMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var cam = playerData as CameraControllor;
            if (cam == null) return;

            Vector2 offset = Vector2.zero;
            float roll = 0f;
            float amplitude = 0f;
            float rollAmplitude = 0f;
            int count = playable.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;

                Playable input = playable.GetInput(i);
                CameraShakeBehaviour shake = ((ScriptPlayable<CameraShakeBehaviour>)input).GetBehaviour();
                float t = (float)input.GetTime() * shake.frequency;
                offset += w * shake.amplitude * new Vector2(Mathf.PerlinNoise(t, 0.1f) * 2f - 1f, Mathf.PerlinNoise(0.7f, t) * 2f - 1f);
                roll += w * shake.rollAmplitude * (Mathf.PerlinNoise(t, 0.4f) * 2f - 1f);
                amplitude += w * Mathf.Abs(shake.amplitude);
                rollAmplitude += w * Mathf.Abs(shake.rollAmplitude);
            }

            cam.SetCinematicShake(offset, roll, amplitude, rollAmplitude);
        }
    }
}
