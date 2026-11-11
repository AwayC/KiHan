namespace KiHan.Asset
{
    /// <summary>
    /// 资源加载方式：播放器恒走 AB 包；编辑器默认直接读工程资源 (菜单「KiHan/资源/编辑器使用 AB 包」切换)。
    /// </summary>
    public static class AssetMode
    {
        public const string EditorPrefKey = "KiHan.Asset.UseBundles";

#if UNITY_EDITOR
        public static bool UseBundles => UnityEditor.EditorPrefs.GetBool(EditorPrefKey, false);
#else
        public static bool UseBundles => true;
#endif
    }
}
