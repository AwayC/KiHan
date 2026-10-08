using System;
using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 全局统一资源管理中心
    /// 提供统一的资产寻址、缓存与生命周期释放接口
    /// 底层可通过 SetProvider 随时平滑切换为 Addressables 或 AssetBundle
    /// </summary>
    public class AssetManager : UnitySingleton<AssetManager>
    {
        private IAssetProvider _provider;

        public IAssetProvider ActiveProvider => _provider;

        protected override void Awake()
        {
            base.Awake();
            if (_provider == null)
            {
                var resProvider = new ResourcesAssetProvider(this);
                _provider = resProvider;
            }
        }

        /// <summary>
        /// 切换或自定义底层资源提供器 (如后续切换为 AddressablesAssetProvider)
        /// </summary>
        public void SetProvider(IAssetProvider provider)
        {
            _provider?.Clear();
            _provider = provider;
        }

        /// <summary>
        /// 同步加载资产
        /// </summary>
        public T Load<T>(string address) where T : UnityEngine.Object
        {
            if (_provider == null) _provider = new ResourcesAssetProvider(this);
            return _provider.LoadAsset<T>(address);
        }

        /// <summary>
        /// 异步加载资产 (推荐)
        /// </summary>
        public void LoadAsync<T>(string address, Action<T> onComplete) where T : UnityEngine.Object
        {
            if (_provider == null) _provider = new ResourcesAssetProvider(this);
            _provider.LoadAssetAsync(address, onComplete);
        }

        /// <summary>
        /// 加载指定目录/图集下的全部子资产
        /// </summary>
        public T[] LoadAll<T>(string address) where T : UnityEngine.Object
        {
            if (_provider == null) _provider = new ResourcesAssetProvider(this);
            return _provider.LoadAllAssets<T>(address);
        }

        /// <summary>
        /// 释放资产引用
        /// </summary>
        public void Release(string address)
        {
            _provider?.ReleaseAsset(address);
        }

        /// <summary>
        /// 释放资产引用
        /// </summary>
        public void Release(UnityEngine.Object asset)
        {
            _provider?.ReleaseAsset(asset);
        }

        /// <summary>
        /// 触发底层清理无用资产
        /// </summary>
        public void UnloadUnused()
        {
            _provider?.UnloadUnusedAssets();
        }

        /// <summary>
        /// 清理全局资源缓存
        /// </summary>
        public void Clear()
        {
            _provider?.Clear();
        }
    }
}
