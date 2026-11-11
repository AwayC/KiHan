using System;
using System.Collections.Generic;
using KiHan.Asset;

namespace KiHan.HotUpdate
{
    /// <summary>
    /// 热更判定 (纯函数，便于测试)
    /// </summary>
    public static class HotUpdatePlan
    {
        /// <summary>持久目录的清单可用：同一整包版本且资源版本比首包新</summary>
        public static bool UsePersistent(BundleManifest builtin, BundleManifest persistent, string appVersion)
        {
            return persistent != null && persistent.appVersion == appVersion && persistent.resVersion > builtin.resVersion;
        }

        /// <summary>远端有同一整包版本的更新资源</summary>
        public static bool ShouldUpdate(BundleManifest local, VersionInfo remote, string appVersion)
        {
            return remote.appVersion == appVersion && remote.resVersion > local.resVersion;
        }

        /// <summary>需要下载的包 (文件名含内容哈希，本地存在即内容一致)</summary>
        public static List<BundleInfo> Missing(BundleManifest remote, Func<string, bool> fileExists)
        {
            var result = new List<BundleInfo>();
            foreach (BundleInfo info in remote.bundles)
            {
                if (!fileExists(info.file)) result.Add(info);
            }
            return result;
        }
    }
}
