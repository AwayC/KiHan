using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KiHan.Asset
{
    /// <summary>
    /// AssetBundle 资源提供器：按清单的地址表找包与包内资源名；包加载后在进程内常驻，不卸载。
    /// 包文件优先读持久目录 (热更)，没有再读首包目录。
    /// </summary>
    public sealed class BundleAssetProvider : IAssetProvider
    {
        private readonly BundleManifest _manifest;
        private readonly Dictionary<string, AssetInfo> _assets = new Dictionary<string, AssetInfo>();
        private readonly AssetBundle[] _loaded;

        public BundleAssetProvider(BundleManifest manifest)
        {
            _manifest = manifest;
            _loaded = new AssetBundle[manifest.bundles.Length];
            foreach (AssetInfo asset in manifest.assets) _assets[asset.address] = asset;
        }

        public T LoadAsset<T>(string address) where T : Object
        {
            if (!_assets.TryGetValue(AssetAddress.Normalize(address), out AssetInfo info)) return null;
            AssetBundle bundle = LoadBundle(info.bundle);
            if (bundle == null) return null;
            foreach (string name in info.names)
            {
                T asset = bundle.LoadAsset<T>(name);
                if (asset != null) return asset;
            }
            return null;
        }

        public T[] LoadAllAssets<T>(string address) where T : Object
        {
            if (!_assets.TryGetValue(AssetAddress.Normalize(address), out AssetInfo info)) return Array.Empty<T>();
            AssetBundle bundle = LoadBundle(info.bundle);
            if (bundle == null) return Array.Empty<T>();
            var result = new List<T>();
            foreach (string name in info.names) result.AddRange(bundle.LoadAssetWithSubAssets<T>(name));
            return result.ToArray();
        }

        public void Clear()
        {
            Resources.UnloadUnusedAssets();
        }

        private AssetBundle LoadBundle(int index)
        {
            if (_loaded[index] != null) return _loaded[index];
            BundleInfo info = _manifest.bundles[index];
            foreach (int dep in info.deps)
            {
                if (_loaded[dep] == null) LoadSingle(dep);
            }
            return LoadSingle(index);
        }

        private AssetBundle LoadSingle(int index)
        {
            BundleInfo info = _manifest.bundles[index];
            string path = BundlePaths.PersistentRoot + "/" + info.file;
            if (!File.Exists(path)) path = BundlePaths.BuiltinRoot + "/" + info.file;
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) Debug.LogError($"[BundleAssetProvider] 加载包失败: {path}");
            _loaded[index] = bundle;
            return bundle;
        }
    }
}
