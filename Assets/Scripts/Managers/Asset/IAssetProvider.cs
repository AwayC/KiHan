using System;
using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 统一资源加载提供器接口
    /// 解耦底层具体实现 (Resources, Addressables, AssetBundle, 或 EditorSimulation)
    /// </summary>
    public interface IAssetProvider
    {
        /// <summary>
        /// 同步加载资产 (建议仅在局外加载或确定预热阶段使用)
        /// </summary>
        T LoadAsset<T>(string address) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载资产 (推荐加载方式)
        /// </summary>
        void LoadAssetAsync<T>(string address, Action<T> onComplete) where T : UnityEngine.Object;

        /// <summary>
        /// 加载指定目录/图集下的全部子资产 (如 SpriteAtlas)
        /// </summary>
        T[] LoadAllAssets<T>(string address) where T : UnityEngine.Object;

        /// <summary>
        /// 释放指定地址资产的引用计数
        /// </summary>
        void ReleaseAsset(string address);

        /// <summary>
        /// 释放指定资产实例的引用计数
        /// </summary>
        void ReleaseAsset(UnityEngine.Object asset);

        /// <summary>
        /// 触发底层清理未使用的未被引用资产
        /// </summary>
        void UnloadUnusedAssets();

        /// <summary>
        /// 清空所有缓存与加载句柄
        /// </summary>
        void Clear();
    }
}
