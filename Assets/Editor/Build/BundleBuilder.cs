using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KiHan.Asset;
using UnityEditor;
using UnityEngine;

namespace KiHan.Editor
{
    /// <summary>
    /// AB 包构建：Res 与 Lua 目录下的全部资源 (连同依赖) 按目录规则分包，输出到 Build/Bundles/&lt;平台&gt;/
    /// (包文件名带内容哈希 + manifest_N.json + version.json，该目录即热更服务器目录)；
    /// InstallBuiltin 把当前版本复制为首包。
    /// </summary>
    public static class BundleBuilder
    {
        private const string ResFolder = "Assets/AssetPackages/Res";
        private const string LuaFolder = "Assets/Lua";
        private const string VersionFile = "version.json";
        private const string MenuRoot = "KiHan/资源/";
        private const string UseBundlesMenu = MenuRoot + "编辑器使用 AB 包";

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

        public static string OutputDir(BuildTarget target) => $"{ProjectRoot}/Build/Bundles/{target}";

        [MenuItem(MenuRoot + "构建 AB 包/Android")]
        private static void BuildAndroid() => Debug.Log("[BundleBuilder] " + Build(BuildTarget.Android));

        [MenuItem(MenuRoot + "构建 AB 包/编辑器验证 (Win64)")]
        private static void BuildWin64() => Debug.Log("[BundleBuilder] " + Build(BuildTarget.StandaloneWindows64));

        [MenuItem(MenuRoot + "写入首包/Android")]
        private static void InstallAndroid() => Debug.Log("[BundleBuilder] " + InstallBuiltin(BuildTarget.Android));

        [MenuItem(MenuRoot + "写入首包/编辑器验证 (Win64)")]
        private static void InstallWin64() => Debug.Log("[BundleBuilder] " + InstallBuiltin(BuildTarget.StandaloneWindows64));

        [MenuItem(UseBundlesMenu)]
        private static void ToggleUseBundles()
        {
            EditorPrefs.SetBool(AssetMode.EditorPrefKey, !AssetMode.UseBundles);
        }

        [MenuItem(UseBundlesMenu, true)]
        private static bool ToggleUseBundlesValidate()
        {
            Menu.SetChecked(UseBundlesMenu, AssetMode.UseBundles);
            return true;
        }

        public static string Build(BuildTarget target)
        {
            // 1. 根资源
            var folders = new List<string> { ResFolder };
            if (AssetDatabase.IsValidFolder(LuaFolder)) folders.Add(LuaFolder);
            string[] roots = AssetDatabase.FindAssets("", folders.ToArray())
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

            // 2. 闭包 + 3. 分包
            var bundleAssets = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var bundleOf = new Dictionary<string, string>();
            foreach (string path in roots.Concat(AssetDatabase.GetDependencies(roots, true)))
            {
                if (bundleOf.ContainsKey(path) || !IsPackable(path)) continue;
                string bundle = BundleNameOf(path);
                bundleOf[path] = bundle;
                if (!bundleAssets.TryGetValue(bundle, out SortedSet<string> set)) bundleAssets[bundle] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add(path);
            }

            AssetBundleBuild[] builds = bundleAssets.Select(kv => new AssetBundleBuild { assetBundleName = kv.Key, assetNames = kv.Value.ToArray() }).ToArray();

            // 4. 构建
            string staging = $"{ProjectRoot}/Library/BundleStaging/{target}";
            Directory.CreateDirectory(staging);
            AssetBundleManifest unityManifest = BuildPipeline.BuildAssetBundles(staging, builds,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
            if (unityManifest == null) throw new Exception($"[BundleBuilder] {target} 构建失败，详见控制台");

            // 5. 包信息 (按包名排序，下标即 deps 引用)
            string output = OutputDir(target);
            Directory.CreateDirectory(output);
            string[] names = bundleAssets.Keys.ToArray();
            var indexOf = new Dictionary<string, int>();
            for (int i = 0; i < names.Length; i++) indexOf[names[i]] = i;

            var bundles = new BundleInfo[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string src = $"{staging}/{names[i]}";
                string md5 = BundleHash.Md5File(src);
                bundles[i] = new BundleInfo
                {
                    name = names[i],
                    file = $"{names[i]}_{md5.Substring(0, 8)}.ab",
                    md5 = md5,
                    size = new FileInfo(src).Length,
                    deps = unityManifest.GetAllDependencies(names[i]).Select(d => indexOf[d]).OrderBy(d => d).ToArray(),
                };
            }

            // 6. 地址表 (同地址的多个文件合并)
            var byAddress = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string root in roots)
            {
                if (!bundleOf.ContainsKey(root) || !AssetAddress.TryFromAssetPath(root, out string address)) continue;
                if (!byAddress.TryGetValue(address, out List<string> list)) byAddress[address] = list = new List<string>();
                list.Add(root);
            }
            var assets = new List<AssetInfo>();
            foreach (KeyValuePair<string, List<string>> kv in byAddress)
            {
                int bundle = indexOf[bundleOf[kv.Value[0]]];
                if (kv.Value.Any(p => indexOf[bundleOf[p]] != bundle)) throw new Exception($"[BundleBuilder] 地址 {kv.Key} 的文件被分到不同的包");
                assets.Add(new AssetInfo { address = kv.Key, bundle = bundle, names = kv.Value.Select(p => p.ToLowerInvariant()).ToArray() });
            }

            // 7. 版本号
            BundleManifest old = ReadCurrent(output, out _);
            var manifest = new BundleManifest
            {
                appVersion = PlayerSettings.bundleVersion,
                resVersion = old != null ? old.resVersion + 1 : 1,
                bundles = bundles,
                assets = assets.ToArray(),
            };
            if (old != null && SameContent(old, manifest)) return $"无变化 resVersion={old.resVersion}";

            // 8. 写文件
            var oldFiles = new HashSet<string>(old != null ? old.bundles.Select(b => b.file) : Enumerable.Empty<string>());
            int changed = 0;
            foreach (BundleInfo info in bundles)
            {
                if (!oldFiles.Contains(info.file)) changed++;
                string dst = $"{output}/{info.file}";
                if (!File.Exists(dst)) File.Copy($"{staging}/{info.name}", dst);
            }

            string manifestName = $"manifest_{manifest.resVersion}.json";
            byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest, true));
            File.WriteAllBytes($"{output}/{manifestName}", manifestBytes);
            var version = new VersionInfo
            {
                appVersion = manifest.appVersion,
                resVersion = manifest.resVersion,
                manifest = manifestName,
                manifestMd5 = BundleHash.Md5(manifestBytes),
            };
            File.WriteAllText($"{output}/{VersionFile}", JsonUtility.ToJson(version, true), new UTF8Encoding(false));

            var keep = new HashSet<string>(bundles.Select(b => b.file)) { manifestName, VersionFile };
            foreach (string file in Directory.GetFiles(output))
            {
                string name = Path.GetFileName(file);
                bool managed = name.EndsWith(".ab", StringComparison.Ordinal) || (name.StartsWith("manifest_", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal));
                if (managed && !keep.Contains(name)) File.Delete(file);
            }

            return $"resVersion={manifest.resVersion} bundles={bundles.Length} assets={manifest.assets.Length} changed={changed}";
        }

        public static string InstallBuiltin(BuildTarget target)
        {
            string output = OutputDir(target);
            BundleManifest manifest = ReadCurrent(output, out string manifestPath);
            if (manifest == null) throw new Exception($"[BundleBuilder] 没有 {target} 的构建结果，先执行构建");

            string dst;
            switch (target)
            {
                case BuildTarget.Android: dst = $"{ProjectRoot}/Assets/StreamingAssets/bundles"; break;
                case BuildTarget.StandaloneWindows64: dst = $"{ProjectRoot}/Build/Builtin/StandaloneWindows64"; break;
                default: throw new NotSupportedException($"[BundleBuilder] 不支持的首包平台 {target}");
            }

            if (Directory.Exists(dst)) Directory.Delete(dst, true);
            Directory.CreateDirectory(dst);
            foreach (BundleInfo info in manifest.bundles) File.Copy($"{output}/{info.file}", $"{dst}/{info.file}");
            File.Copy(manifestPath, $"{dst}/{BundlePaths.ManifestFile}");

            if (target == BuildTarget.Android) AssetDatabase.Refresh();
            return $"首包 {target} resVersion={manifest.resVersion} → {dst}";
        }

        private static BundleManifest ReadCurrent(string output, out string manifestPath)
        {
            manifestPath = null;
            string versionPath = $"{output}/{VersionFile}";
            if (!File.Exists(versionPath)) return null;
            VersionInfo version = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(versionPath));
            manifestPath = $"{output}/{version.manifest}";
            return File.Exists(manifestPath) ? JsonUtility.FromJson<BundleManifest>(File.ReadAllText(manifestPath)) : null;
        }

        private static bool SameContent(BundleManifest a, BundleManifest b)
        {
            if (!a.bundles.Select(x => x.file).SequenceEqual(b.bundles.Select(x => x.file))) return false;
            if (a.assets.Length != b.assets.Length) return false;
            for (int i = 0; i < a.assets.Length; i++)
            {
                if (JsonUtility.ToJson(a.assets[i]) != JsonUtility.ToJson(b.assets[i])) return false;
            }
            return true;
        }

        private static bool IsPackable(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext != ".cs" && ext != ".dll" && ext != ".unity";
        }

        /// <summary>
        /// 分包规则 (自上而下第一条命中)
        /// </summary>
        public static string BundleNameOf(string assetPath)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            if (ext == ".shader" || ext == ".shadervariants" || ext == ".cginc" || ext == ".hlsl") return "shaders";

            string rel = assetPath.Substring("Assets/".Length);
            if (rel.StartsWith("Lua/", StringComparison.Ordinal)) return "lua";
            if (rel.StartsWith("AssetPackages/Res/Config/", StringComparison.Ordinal)) return "config";
            if (TrySub(rel, "AssetPackages/Res/UI/Panels/", out string module)) return "ui_" + module;
            if (rel.StartsWith("AssetPackages/Res/UI/", StringComparison.Ordinal)) return "ui_common";
            if (TrySub(rel, "AssetPackages/Res/Maps/", out string map) || TrySub(rel, "AssetPackages/Maps/", out map)) return "map_" + map;
            if (rel.StartsWith("AssetPackages/Res/Effect/", StringComparison.Ordinal) || rel.StartsWith("AssetPackages/Effects/", StringComparison.Ordinal)) return "effect_common";
            if (rel.StartsWith("AssetPackages/Res/Render/", StringComparison.Ordinal)) return "render";
            if (TrySub(rel, "AssetPackages/Characters/", out string pkg)) return "char_" + pkg;
            if (TrySub(rel, "AssetPackages/GUI/", out string sub)) return "gui_" + sub;
            return "shared";
        }

        /// <summary>prefix 之后的第一级目录名 (必须是目录，不能是文件)，转成合法包名</summary>
        private static bool TrySub(string rel, string prefix, out string name)
        {
            name = null;
            if (!rel.StartsWith(prefix, StringComparison.Ordinal)) return false;
            int slash = rel.IndexOf('/', prefix.Length);
            if (slash < 0) return false;
            var sb = new StringBuilder();
            foreach (char c in rel.Substring(prefix.Length, slash - prefix.Length).ToLowerInvariant())
            {
                sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
            }
            name = sb.ToString();
            return true;
        }
    }
}
