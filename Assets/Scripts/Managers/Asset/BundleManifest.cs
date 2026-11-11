using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KiHan.Asset
{
    /// <summary>
    /// AB 包清单 (manifest_N.json / manifest.json)：包列表 + 地址表，JsonUtility 读写
    /// </summary>
    [Serializable]
    public sealed class BundleManifest
    {
        public string appVersion;
        public int resVersion;
        public BundleInfo[] bundles;
        public AssetInfo[] assets;
    }

    [Serializable]
    public sealed class BundleInfo
    {
        /// <summary>包名 (不含哈希)</summary>
        public string name;
        /// <summary>包文件名 name_md5前8位.ab；文件名含内容哈希，同名即同内容</summary>
        public string file;
        public string md5;
        public long size;
        /// <summary>全部依赖 (已展开) 在 bundles 中的下标</summary>
        public int[] deps;
    }

    [Serializable]
    public sealed class AssetInfo
    {
        public string address;
        public int bundle;
        /// <summary>包内资源名 (小写的完整资源路径)；同地址不同扩展名的文件各一项</summary>
        public string[] names;
    }

    /// <summary>
    /// 服务器上的 version.json：指向当前清单
    /// </summary>
    [Serializable]
    public sealed class VersionInfo
    {
        public string appVersion;
        public int resVersion;
        public string manifest;
        public string manifestMd5;
    }

    public static class BundleHash
    {
        /// <summary>32 位小写十六进制 MD5</summary>
        public static string Md5(byte[] data)
        {
            using var md5 = MD5.Create();
            return ToHex(md5.ComputeHash(data));
        }

        public static string Md5File(string path)
        {
            using var md5 = MD5.Create();
            using FileStream stream = File.OpenRead(path);
            return ToHex(md5.ComputeHash(stream));
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
