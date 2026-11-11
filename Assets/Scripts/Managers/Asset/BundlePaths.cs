using System.IO;
using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// AB 包目录：首包 (随安装包) 与持久目录 (热更下载) 布局相同：manifest.json + 平铺的包文件
    /// </summary>
    public static class BundlePaths
    {
        public const string ManifestFile = "manifest.json";

        /// <summary>包的平台目录名 (与 BuildTarget 名一致)</summary>
        public static string Platform
        {
            get
            {
#if UNITY_EDITOR
                return "StandaloneWindows64";
#elif UNITY_ANDROID
                return "Android";
#else
                return Application.platform.ToString();
#endif
            }
        }

        /// <summary>首包目录：编辑器为 &lt;工程&gt;/Build/Builtin/StandaloneWindows64，播放器为 StreamingAssets/bundles</summary>
        public static string BuiltinRoot
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/Build/Builtin/" + Platform;
#else
                return Application.streamingAssetsPath + "/bundles";
#endif
            }
        }

        /// <summary>热更下载目录</summary>
        public static string PersistentRoot => Application.persistentDataPath + "/bundles";
    }
}
