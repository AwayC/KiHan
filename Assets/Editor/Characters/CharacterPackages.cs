using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KiHan.Config;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KiHan.Editor
{
    /// <summary>
    /// 角色包工具：按目录约定收集清单 (Character.asset) 的配置、动画与资源表，并提供注册 / 校验菜单
    ///
    /// 收集规则 (以清单所在目录为包根目录)：
    /// - Config/actions.json、skills.json、spawns.json、buffs.json
    /// - 包内全部 AnimationFrameData (按路径排序)
    /// - 包内全部 prefab：key = 相对包根目录的路径 (不含扩展名)，如 effect/lxw/ball
    /// - UI/ 下图片的 Sprite：key = 相对路径；多图切片为 相对路径#子图名
    /// 包内文件新增 / 删除 / 移动时自动刷新对应清单，也可用菜单手动刷新
    /// </summary>
    public static class CharacterPackages
    {
        public const string ConfigFolder = "Config";
        public const string UIFolder = "UI";

        private const string RegistryPath = "Assets/AssetPackages/Resources/Config/CharacterRegistry.asset";

        /// <summary>
        /// 工程内全部角色包清单 (按路径排序)
        /// </summary>
        public static List<CharacterManifest> FindManifests()
        {
            return AssetDatabase.FindAssets("t:" + nameof(CharacterManifest))
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterManifest>)
                .Where(m => m != null)
                .ToList();
        }

        /// <summary>
        /// 包根目录 (清单所在目录，工程相对路径)
        /// </summary>
        public static string PackageRoot(CharacterManifest manifest)
        {
            return Path.GetDirectoryName(AssetDatabase.GetAssetPath(manifest))?.Replace('\\', '/');
        }

        public static CharacterRegistry LoadRegistry()
        {
            return AssetDatabase.LoadAssetAtPath<CharacterRegistry>(RegistryPath);
        }

        /// <summary>
        /// 按目录重新收集清单内容；内容未变化时不写盘 (避免导入回调循环)
        /// </summary>
        /// <returns>是否有变化</returns>
        public static bool Refresh(CharacterManifest manifest)
        {
            string root = PackageRoot(manifest);
            if (string.IsNullOrEmpty(root)) return false;

            TextAsset actions = LoadConfig(root, "actions");
            TextAsset skills = LoadConfig(root, "skills");
            TextAsset spawns = LoadConfig(root, "spawns");
            TextAsset buffs = LoadConfig(root, "buffs");

            List<AnimationFrameData> animations = AssetDatabase.FindAssets("t:AnimationFrameData", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationFrameData>)
                .Where(a => a != null)
                .ToList();

            List<CharacterResource> resources = CollectResources(root);

            bool changed = manifest.actions != actions || manifest.skills != skills || manifest.spawns != spawns || manifest.buffs != buffs
                           || !manifest.animations.SequenceEqual(animations)
                           || !SameResources(manifest.resources, resources);
            if (!changed) return false;

            Undo.RecordObject(manifest, "刷新角色包");
            manifest.actions = actions;
            manifest.skills = skills;
            manifest.spawns = spawns;
            manifest.buffs = buffs;
            manifest.animations = animations;
            manifest.resources = resources;
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
            return true;
        }

        private static TextAsset LoadConfig(string root, string name)
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{ConfigFolder}/{name}.json");
        }

        private static List<CharacterResource> CollectResources(string root)
        {
            var resources = new List<CharacterResource>();

            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                resources.Add(new CharacterResource { key = KeyOf(root, path), asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) });
            }

            string uiRoot = $"{root}/{UIFolder}";
            if (AssetDatabase.IsValidFolder(uiRoot))
            {
                foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { uiRoot }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
                {
                    Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                    string key = KeyOf(root, path);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    bool multiple = importer != null && importer.spriteImportMode == SpriteImportMode.Multiple;
                    foreach (Sprite sprite in sprites)
                    {
                        resources.Add(new CharacterResource { key = multiple ? $"{key}#{sprite.name}" : key, asset = sprite });
                    }
                }
            }
            return resources;
        }

        /// <summary>
        /// 相对包根目录的路径，不含扩展名
        /// </summary>
        public static string KeyOf(string root, string assetPath)
        {
            string relative = assetPath.Substring(root.Length + 1);
            string ext = Path.GetExtension(relative);
            return ext.Length > 0 ? relative.Substring(0, relative.Length - ext.Length) : relative;
        }

        private static bool SameResources(List<CharacterResource> a, List<CharacterResource> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].key != b[i].key || a[i].asset != b[i].asset) return false;
            }
            return true;
        }

        /// <summary>
        /// 资源路径所属的角色包清单 (向上查找最近的 Character.asset)，不属于任何包返回 null
        /// </summary>
        public static string FindManifestPath(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets", StringComparison.Ordinal))
            {
                string candidate = $"{dir}/{CharacterManifest.FileName}.asset";
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            }
            return null;
        }

        [MenuItem("KiHan/角色/刷新全部角色包")]
        private static void RefreshAll()
        {
            int changed = 0;
            List<CharacterManifest> manifests = FindManifests();
            foreach (CharacterManifest manifest in manifests)
            {
                if (Refresh(manifest)) changed++;
            }
            Debug.Log($"[CharacterPackages] 共 {manifests.Count} 个角色包，{changed} 个有更新");
        }

        [MenuItem("KiHan/角色/校验全部角色包")]
        public static void ValidateAll()
        {
            CharacterRegistry registry = LoadRegistry();
            var registered = new HashSet<CharacterManifest>();
            if (registry != null)
            {
                foreach (CharacterRegistryEntry entry in registry.characters)
                {
                    if (entry.manifest != null) registered.Add(entry.manifest);
                }
            }

            int failed = 0;
            foreach (CharacterManifest manifest in FindManifests())
            {
                List<string> problems = CharacterValidator.Validate(manifest);
                string tag = registered.Contains(manifest) ? "已注册" : "未注册";
                if (problems.Count == 0)
                {
                    Debug.Log($"[CharacterPackages] [OK] {manifest.id} {manifest.displayName} ({tag}): {PackageRoot(manifest)}");
                    continue;
                }
                failed++;
                Debug.LogError($"[CharacterPackages] {manifest.id} {manifest.displayName} ({tag}) 有 {problems.Count} 个问题:\n- {string.Join("\n- ", problems)}", manifest);
            }
            if (failed == 0) Debug.Log("[CharacterPackages] 全部角色包校验通过");
        }

        [MenuItem("Assets/KiHan/在此目录新建角色包", false, 30)]
        private static void CreatePackageHere()
        {
            string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(folder)) return;
            if (!AssetDatabase.IsValidFolder(folder)) folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');

            string path = $"{folder}/{CharacterManifest.FileName}.asset";
            if (File.Exists(path))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<CharacterManifest>(path));
                return;
            }

            if (!AssetDatabase.IsValidFolder($"{folder}/{ConfigFolder}")) AssetDatabase.CreateFolder(folder, ConfigFolder);
            var manifest = ScriptableObject.CreateInstance<CharacterManifest>();
            manifest.displayName = Path.GetFileName(folder);
            AssetDatabase.CreateAsset(manifest, path);
            Refresh(manifest);
            Selection.activeObject = manifest;
        }

        /// <summary>
        /// 登记到角色注册表 (已登记则跳过)
        /// </summary>
        public static void Register(CharacterManifest manifest)
        {
            CharacterRegistry registry = LoadRegistry();
            if (registry == null)
            {
                Debug.LogError($"[CharacterPackages] 找不到角色注册表: {RegistryPath}");
                return;
            }
            if (registry.characters.Any(e => e.manifest == manifest)) return;

            Undo.RecordObject(registry, "登记角色包");
            registry.characters.Add(new CharacterRegistryEntry { enabled = true, manifest = manifest });
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }
    }

    /// <summary>
    /// 包内文件变化时自动刷新所属清单 (延迟到导入结束后执行，刷新无变化时不写盘，不会循环)
    /// </summary>
    internal class CharacterPackagePostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            Collect(imported);
            Collect(deleted);
            Collect(moved);
            Collect(movedFrom);
            if (Pending.Count == 0) return;

            EditorApplication.delayCall -= Flush;
            EditorApplication.delayCall += Flush;
        }

        private static void Collect(string[] paths)
        {
            foreach (string path in paths)
            {
                string manifestPath = path.EndsWith($"/{CharacterManifest.FileName}.asset", StringComparison.Ordinal)
                    ? path
                    : CharacterPackages.FindManifestPath(path);
                if (manifestPath != null) Pending.Add(manifestPath);
            }
        }

        private static void Flush()
        {
            string[] paths = Pending.ToArray();
            Pending.Clear();
            foreach (string path in paths)
            {
                var manifest = AssetDatabase.LoadAssetAtPath<CharacterManifest>(path);
                if (manifest != null) CharacterPackages.Refresh(manifest);
            }
        }
    }

    [CustomEditor(typeof(CharacterManifest))]
    internal class CharacterManifestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var manifest = (CharacterManifest)target;
            EditorGUILayout.HelpBox($"包根目录: {CharacterPackages.PackageRoot(manifest)}\n配置、动画与资源表按目录自动收集，只需填写身份信息。", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新")) CharacterPackages.Refresh(manifest);
                if (GUILayout.Button("校验"))
                {
                    List<string> problems = CharacterValidator.Validate(manifest);
                    if (problems.Count == 0) Debug.Log($"[CharacterPackages] [OK] {manifest.id} {manifest.displayName} 校验通过", manifest);
                    else Debug.LogError($"[CharacterPackages] {manifest.id} {manifest.displayName} 有 {problems.Count} 个问题:\n- {string.Join("\n- ", problems)}", manifest);
                }
                if (GUILayout.Button("登记到注册表")) CharacterPackages.Register(manifest);
            }

            EditorGUILayout.Space(4);
            DrawDefaultInspector();
        }
    }
}
