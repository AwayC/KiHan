using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 全局统一资源管理中心：按地址加载资源，底层由 SetProvider 指定 (编辑器模拟 / AssetBundle)。
    /// 编辑器未启用 AB 模式时自动使用 EditorAssetProvider；AB 模式由启动流程在热更结束后设置。
    /// </summary>
    public class AssetManager : UnitySingleton<AssetManager>
    {
        private IAssetProvider _provider;

        public IAssetProvider ActiveProvider => _provider;

        /// <summary>
        /// 切换底层资源提供器
        /// </summary>
        public void SetProvider(IAssetProvider provider)
        {
            _provider?.Clear();
            _provider = provider;
        }

        /// <summary>
        /// 同步加载资产；找不到时打警告
        /// </summary>
        public T Load<T>(string address) where T : Object
        {
            T asset = TryLoad<T>(address);
            if (asset == null && _provider != null) Debug.LogWarning($"[AssetManager] 找不到资源 {address} ({typeof(T).Name})");
            return asset;
        }

        /// <summary>
        /// 同步加载资产；找不到返回 null 不打日志 (用于先查 A 再查 B 的回退)
        /// </summary>
        public T TryLoad<T>(string address) where T : Object
        {
            return EnsureProvider() ? _provider.LoadAsset<T>(address) : null;
        }

        /// <summary>
        /// 加载该地址文件内的全部子资产
        /// </summary>
        public T[] LoadAll<T>(string address) where T : Object
        {
            return EnsureProvider() ? _provider.LoadAllAssets<T>(address) : System.Array.Empty<T>();
        }

        /// <summary>
        /// 清理未被引用的资产
        /// </summary>
        public void Clear()
        {
            _provider?.Clear();
        }

        private bool EnsureProvider()
        {
#if UNITY_EDITOR
            if (_provider == null && (!Application.isPlaying || !AssetMode.UseBundles)) _provider = new EditorAssetProvider();
#endif
            if (_provider == null) Debug.LogError("[AssetManager] 资源系统尚未初始化");
            return _provider != null;
        }
    }
}
