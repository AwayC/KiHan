using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Config;
using KiHan.Logic;
using UnityEngine;

namespace View.UI
{
    /// <summary>
    /// 技能按键图标缓存：加载阶段按 skills.json 中技能的 icon 与动作指令 skillStage 的 icon 一次性解析，局内只查表
    /// icon 优先作为角色包资源 key 查找 (如 UI/90001_SkillIconAtlas#90001_SkillIconAtlas_0)，
    /// 找不到时按全局 Resources 地址加载 (单图路径，或多图切片 "路径#子图名")
    /// </summary>
    public class SkillIconCache
    {
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public void Preload(SkillController skills, CharacterManifest manifest)
        {
            for (int i = 0; i < skills.Count; i++) Add(skills.GetSkill(i).icon, manifest);
            foreach (string icon in skills.StageIcons) Add(icon, manifest);
        }

        /// <summary>
        /// 直接登记一张图标 (秘卷等不走资源地址的图标)，需在 Preload 之前调用
        /// </summary>
        public void Register(string key, Sprite sprite)
        {
            if (!string.IsNullOrEmpty(key) && sprite != null) _sprites[key] = sprite;
        }

        public void Clear()
        {
            _sprites.Clear();
        }

        public Sprite Get(string address)
        {
            return !string.IsNullOrEmpty(address) && _sprites.TryGetValue(address, out Sprite sprite) ? sprite : null;
        }

        private void Add(string address, CharacterManifest manifest)
        {
            if (string.IsNullOrEmpty(address) || _sprites.ContainsKey(address)) return;
            Sprite sprite = Load(address, manifest);
            if (sprite == null) Debug.LogError($"[SkillIconCache] 找不到技能图标: {address}");
            _sprites[address] = sprite;
        }

        /// <summary>
        /// 按 key 解析图标：先查角色包资源表，再按全局 Resources 地址加载 (加载阶段 / 配置检查使用)
        /// </summary>
        public static Sprite Load(string address, CharacterManifest manifest)
        {
            Sprite packaged = manifest != null ? manifest.FindResource<Sprite>(address) : null;
            if (packaged != null) return packaged;

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
