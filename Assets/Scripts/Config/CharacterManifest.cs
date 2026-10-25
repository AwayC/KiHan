using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KiHan.Config
{
    /// <summary>
    /// 角色包清单：一个文件夹就是一个角色，文件夹根目录放一份本清单 (Character.asset)
    ///
    /// 目录约定 (以清单所在目录为包根目录)：
    /// - Config/actions.json、skills.json、spawns.json、buffs.json
    /// - 任意位置的 AnimationFrameData (资源名即 animKey) 与帧库
    /// - 任意位置的特效 prefab、UI/ 下的图片 (头像、技能图标)
    ///
    /// 包内资源按"相对包根目录的路径 (不含扩展名)"作为 key 引用，多图切片追加 "#子图名"，
    /// 如 effect/lxw/ball、UI/90001_SkillIconAtlas#90001_SkillIconAtlas_0；json 中只写 key，包可以整体移动或改名。
    /// 配置、动画与资源表由编辑器菜单 "KiHan/角色/刷新全部角色包" (或文件变化时自动) 按目录收集，只需手填身份信息。
    /// </summary>
    [CreateAssetMenu(fileName = "Character", menuName = "KiHan/Character Manifest")]
    public class CharacterManifest : ScriptableObject
    {
        public const string FileName = "Character";

        /// <summary>
        /// 生成物、特效对象池等需要全局唯一的资源地址前缀：pkg/{id}/{key}
        /// </summary>
        private const string AddressPrefix = "pkg/";

        [Header("身份 (手动填写)")]
        [Tooltip("角色 ID，全局唯一 (注册表与出战配置按此引用)")]
        public int id;

        [Tooltip("战斗 HUD 显示的角色名")]
        public string displayName;

        [Tooltip("战斗 HUD 头像")]
        public Sprite headshot;

        [Header("配置 (自动收集 Config/ 目录)")]
        public TextAsset actions;
        public TextAsset skills;
        public TextAsset spawns;
        public TextAsset buffs;

        [Header("动画 (自动收集包内全部 AnimationFrameData)")]
        public List<AnimationFrameData> animations = new List<AnimationFrameData>();

        [Header("包内资源表 (自动收集包内 prefab 与 UI/ 下的图片)")]
        public List<CharacterResource> resources = new List<CharacterResource>();

        [NonSerialized] private Dictionary<string, Object> _lookup;
        [NonSerialized] private Dictionary<string, string> _addresses;

        /// <summary>
        /// 按 key 查找包内资源，没有或类型不符返回 null
        /// </summary>
        public T FindResource<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;
            BuildLookup();
            return _lookup.TryGetValue(key, out Object asset) ? asset as T : null;
        }

        /// <summary>
        /// key 对应包内 prefab 时返回其全局唯一地址 (pkg/{id}/{key}，缓存，不产生 GC) 与 prefab
        /// </summary>
        public bool TryGetPrefab(string key, out string address, out GameObject prefab)
        {
            prefab = FindResource<GameObject>(key);
            if (prefab == null)
            {
                address = null;
                return false;
            }

            if (!_addresses.TryGetValue(key, out address))
            {
                address = $"{AddressPrefix}{id}/{key}";
                _addresses[key] = address;
            }
            return true;
        }

        private void BuildLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Object>(resources.Count, StringComparer.Ordinal);
            _addresses = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CharacterResource res in resources)
            {
                if (!string.IsNullOrEmpty(res.key) && res.asset != null) _lookup[res.key] = res.asset;
            }
        }

        private void OnValidate()
        {
            // 资源表在编辑器中被修改 (刷新 / 手动编辑) 后重建索引
            _lookup = null;
            _addresses = null;
        }
    }

    [Serializable]
    public struct CharacterResource
    {
        [Tooltip("相对包根目录的路径 (不含扩展名)，多图切片追加 #子图名")]
        public string key;

        public Object asset;
    }
}
