#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 编辑器模拟模式：按地址直接读工程里的资源 (AssetDatabase)，不需要先构建 AB 包。
    /// </summary>
    public sealed class EditorAssetProvider : IAssetProvider
    {
        private const string ResFolder = "Assets/AssetPackages/Res";
        private const string LuaFolder = "Assets/Lua";

        private Dictionary<string, List<string>> _index;

        public T LoadAsset<T>(string address) where T : Object
        {
            List<string> paths = Find(address);
            if (paths == null) return null;
            foreach (string path in paths)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        public T[] LoadAllAssets<T>(string address) where T : Object
        {
            List<string> paths = Find(address);
            if (paths == null) return System.Array.Empty<T>();
            var result = new List<T>();
            foreach (string path in paths) result.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>());
            return result.ToArray();
        }

        public void Clear()
        {
            Resources.UnloadUnusedAssets();
        }

        private List<string> Find(string address)
        {
            string key = AssetAddress.Normalize(address);
            if (_index == null) BuildIndex();
            if (_index.TryGetValue(key, out List<string> paths)) return paths;
            BuildIndex();
            return _index.TryGetValue(key, out paths) ? paths : null;
        }

        private void BuildIndex()
        {
            _index = new Dictionary<string, List<string>>();
            string[] folders = AssetDatabase.IsValidFolder(LuaFolder) ? new[] { ResFolder, LuaFolder } : new[] { ResFolder };
            foreach (string guid in AssetDatabase.FindAssets("", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (!AssetAddress.TryFromAssetPath(path, out string address)) continue;
                if (!_index.TryGetValue(address, out List<string> list)) _index[address] = list = new List<string>();
                if (!list.Contains(path)) list.Add(path);
            }
        }
    }
}
#endif
