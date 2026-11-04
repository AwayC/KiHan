using UnityEngine;

namespace KiHan.View.Audio
{
    /// <summary>
    /// 音效配置：一组可随机的音频、类别、音量、音高区间与防刷屏间隔
    /// 动作 Sound 事件 / 约定音效 / 特效伴随音效都引用本资源；只有 AudioClip 时由 FromClip 包装为默认配置
    /// </summary>
    [CreateAssetMenu(fileName = "SoundCue", menuName = "KiHan/Sound Cue")]
    public class SoundCue : ScriptableObject
    {
        [Tooltip("每次随机取一条 (表现层随机，不影响逻辑)")]
        public AudioClip[] clips;

        public SoundCategory category = SoundCategory.Sfx;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("音高随机区间 (x 最小，y 最大)")]
        public Vector2 pitch = new Vector2(1f, 1f);

        [Tooltip("同一 SoundCue 两次播放的最小间隔 (秒)，防止追帧 / 连段刷屏")]
        public float cooldown = 0.05f;

        /// <summary>
        /// 把单个 AudioClip 包装成默认配置的运行时实例 (调用方负责 Destroy)
        /// </summary>
        public static SoundCue FromClip(AudioClip clip)
        {
            SoundCue cue = CreateInstance<SoundCue>();
            cue.name = clip.name;
            cue.clips = new[] { clip };
            return cue;
        }
    }
}
