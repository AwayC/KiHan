using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 基于 Unity Resources 的默认资源提供器实现
    /// 包含路径标准化、资产缓存与引用计数管理
    /// </summary>
    public class ResourcesAssetProvider : IAssetProvider
    {
        private class AssetEntry
        {
            public UnityEngine.Object Asset;
            public int RefCount;
            public string Address;

            public AssetEntry(string address, UnityEngine.Object asset)
            {
                Address = address;
                Asset = asset;
                RefCount = 1;
            }
        }

        private readonly Dictionary<string, AssetEntry> _addressCache = new();
        private readonly Dictionary<UnityEngine.Object, string> _objectToAddress = new();
        private MonoBehaviour _coroutineHost;

        public ResourcesAssetProvider(MonoBehaviour coroutineHost = null)
        {
            _coroutineHost = coroutineHost;
        }

        public void SetCoroutineHost(MonoBehaviour host)
        {
            _coroutineHost = host;
        }

        private string NormalizeAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return string.Empty;
            string clean = address.Replace("\\", "/").Trim();
            if (clean.StartsWith("/")) clean = clean.Substring(1);
            if (clean.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 7);
            else if (clean.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 6);
            else if (clean.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 4);
            return clean;
        }

        public T LoadAsset<T>(string address) where T : UnityEngine.Object
        {
            string cleanAddress = NormalizeAddress(address);
            if (string.IsNullOrEmpty(cleanAddress)) return null;

            if (_addressCache.TryGetValue(cleanAddress, out var entry))
            {
                entry.RefCount++;
                return entry.Asset as T;
            }

            T asset = Resources.Load<T>(cleanAddress);
            if (asset != null)
            {
                var newEntry = new AssetEntry(cleanAddress, asset);
                _addressCache[cleanAddress] = newEntry;
                _objectToAddress[asset] = cleanAddress;
            }
            else
            {
                Debug.LogWarning($"[ResourcesAssetProvider] 资产加载失败，未找到: {cleanAddress} (原路径: {address})");
            }
            return asset;
        }

        public void LoadAssetAsync<T>(string address, Action<T> onComplete) where T : UnityEngine.Object
        {
            string cleanAddress = NormalizeAddress(address);
            if (string.IsNullOrEmpty(cleanAddress))
            {
                onComplete?.Invoke(null);
                return;
            }

            if (_addressCache.TryGetValue(cleanAddress, out var entry))
            {
                entry.RefCount++;
                onComplete?.Invoke(entry.Asset as T);
                return;
            }

            if (_coroutineHost != null)
            {
                _coroutineHost.StartCoroutine(LoadAssetAsyncRoutine<T>(cleanAddress, onComplete));
            }
            else
            {
                // 无协程宿主时降级同步加载
                T asset = LoadAsset<T>(cleanAddress);
                onComplete?.Invoke(asset);
            }
        }

        private IEnumerator LoadAssetAsyncRoutine<T>(string cleanAddress, Action<T> onComplete) where T : UnityEngine.Object
        {
            ResourceRequest request = Resources.LoadAsync<T>(cleanAddress);
            yield return request;

            T asset = request.asset as T;
            if (asset != null)
            {
                if (!_addressCache.ContainsKey(cleanAddress))
                {
                    var newEntry = new AssetEntry(cleanAddress, asset);
                    _addressCache[cleanAddress] = newEntry;
                    _objectToAddress[asset] = cleanAddress;
                }
                else
                {
                    _addressCache[cleanAddress].RefCount++;
                }
            }
            else
            {
                Debug.LogWarning($"[ResourcesAssetProvider] 异步资产加载失败: {cleanAddress}");
            }
            onComplete?.Invoke(asset);
        }

        public T[] LoadAllAssets<T>(string address) where T : UnityEngine.Object
        {
            string cleanAddress = NormalizeAddress(address);
            if (string.IsNullOrEmpty(cleanAddress)) return Array.Empty<T>();

            return Resources.LoadAll<T>(cleanAddress);
        }

        public void ReleaseAsset(string address)
        {
            string cleanAddress = NormalizeAddress(address);
            if (_addressCache.TryGetValue(cleanAddress, out var entry))
            {
                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    _objectToAddress.Remove(entry.Asset);
                    _addressCache.Remove(cleanAddress);
                }
            }
        }

        public void ReleaseAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (_objectToAddress.TryGetValue(asset, out var address))
            {
                ReleaseAsset(address);
            }
        }

        public void UnloadUnusedAssets()
        {
            Resources.UnloadUnusedAssets();
        }

        public void Clear()
        {
            _addressCache.Clear();
            _objectToAddress.Clear();
            UnloadUnusedAssets();
        }
    }
}
