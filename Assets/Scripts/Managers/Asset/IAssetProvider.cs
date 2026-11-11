namespace KiHan.Asset
{
    /// <summary>
    /// 统一资源加载提供器接口 (编辑器模拟 / AssetBundle)
    /// </summary>
    public interface IAssetProvider
    {
        /// <summary>按地址同步加载；找不到返回 null，不打日志。</summary>
        T LoadAsset<T>(string address) where T : UnityEngine.Object;

        /// <summary>该地址对应文件内的全部子资产 (如切片图集的全部 Sprite)，不是目录。</summary>
        T[] LoadAllAssets<T>(string address) where T : UnityEngine.Object;

        /// <summary>清理未被引用的资产 (Resources.UnloadUnusedAssets)。</summary>
        void Clear();
    }
}
