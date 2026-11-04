using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.View.Cinematic
{
    /// <summary>
    /// 按权重混合各镜头片段，总权重作为与跟随镜头的混合权重 (片段淡入淡出即镜头过渡)
    /// </summary>
    public class CameraShotMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var cam = playerData as CameraControllor;
            if (cam == null) return;

            CinematicContext ctx = CinematicPlayer.Instance != null ? CinematicPlayer.Instance.Current : null;
            if (ctx == null) return;

            float sign = ctx.FacingLeft ? -1f : 1f;
            Vector2 castPoint = ctx.CastPoint;
            Vector2 casterPoint = ctx.Caster != null && ctx.Caster.Character != null ? (Vector2)ctx.Caster.transform.position : castPoint;

            Vector2 center = Vector2.zero;
            float zoom = 0f;
            float roll = 0f;
            float tw = 0f;
            int count = playable.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;

                CameraShotBehaviour shot = ((ScriptPlayable<CameraShotBehaviour>)playable.GetInput(i)).GetBehaviour();
                Vector2 basePoint = shot.target == CameraShotTarget.Caster ? casterPoint : castPoint;
                center += w * (basePoint + new Vector2(shot.offset.x * sign, shot.offset.y));
                zoom += w * shot.zoom;
                roll += w * shot.roll * sign;
                tw += w;
            }

            if (tw > 0f)
            {
                cam.SetCinematicShot(center / tw, zoom / tw, roll / tw, Mathf.Clamp01(tw));
            }
            else
            {
                cam.SetCinematicShot(default, 1f, 0f, 0f);
            }
        }
    }
}
