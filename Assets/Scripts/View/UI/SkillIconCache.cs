using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Logic;
using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 技能按键图标缓存：加载阶段按 skills.json 中技能与各段的 icon 一次性加载，局内只查表
    /// 地址格式：Resources 路径 (单图贴图)，或 "路径#子图名" (多图切片贴图，如 UI/SkillButton/90001_SkillIconAtlas#90001_SkillIconAtlas_0)
    /// </summary>
    public class SkillIconCache
    {
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public void Preload(SkillController skills)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                SkillInfo skill = skills.GetSkill(i);
                Add(skill.icon);
                foreach (SkillStageInfo stage in skill.stages) Add(stage.icon);
            }
        }

        public void Clear()
        {
            _sprites.Clear();
        }

        public Sprite Get(string address)
        {
            return !string.IsNullOrEmpty(address) && _sprites.TryGetValue(address, out Sprite sprite) ? sprite : null;
        }

        private void Add(string address)
        {
            if (string.IsNullOrEmpty(address) || _sprites.ContainsKey(address)) return;
            Sprite sprite = Load(address);
            if (sprite == null) Debug.LogError($"[SkillIconCache] 找不到技能图标: {address}");
            _sprites[address] = sprite;
        }

        /// <summary>
        /// 按地址加载图标 (加载阶段 / 配置检查使用)
        /// </summary>
        public static Sprite Load(string address)
        {
            int split = address.IndexOf('#');
            if (split < 0) return AssetManager.Instance.Load<Sprite>(address);

            string name = address.Substring(split + 1);
            foreach (Sprite sprite in AssetManager.Instance.LoadAll<Sprite>(address.Substring(0, split)))
            {
                if (sprite.name == name) return sprite;
            }
            return null;
        }
    }
}
