using UnityEngine;

namespace KiHan.View.Audio
{
    /// <summary>
    /// 固定约定音效 (Resources/Config/SoundConventions.asset)：按钮点击、命中音；字段为空时不播放
    /// 受击语音按约定 key 放在各角色包内 (HurtVoiceKey)
    /// </summary>
    [CreateAssetMenu(fileName = "SoundConventions", menuName = "KiHan/Sound Conventions")]
    public class SoundConventions : ScriptableObject
    {
        public const string ResourcePath = "Config/SoundConventions";

        /// <summary>角色包内的约定 key：受击语音 (Stun / Launch / Grab 时由受击者播放，可缺省)</summary>
        public const string HurtVoiceKey = "Sound/hurt";

        [Tooltip("按钮按下 (战斗虚拟按键除外)")]
        public SoundCue uiClick;

        [Tooltip("命中：Stun / Lying / Grab / GrabbedHit")]
        public SoundCue hitNormal;

        [Tooltip("命中：Launch")]
        public SoundCue hitLaunch;

        [Tooltip("命中：ArmorBlocked")]
        public SoundCue hitArmorBlocked;

        [Tooltip("暴击时额外叠加")]
        public SoundCue hitCritical;
    }
}
