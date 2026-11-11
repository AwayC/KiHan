using System;

namespace KiHan.Asset
{
    /// <summary>
    /// 资源地址规则：Res 目录下的相对路径 (不带扩展名)；Lua 脚本为 "Lua/" + 相对 Assets/Lua 的路径 (不带扩展名)。
    /// 构建器与编辑器提供器共用。
    /// </summary>
    public static class AssetAddress
    {
        public const string ResRoot = "Assets/AssetPackages/Res/";
        public const string LuaRoot = "Assets/Lua/";
        public const string LuaPrefix = "Lua/";

        public static string Normalize(string address)
        {
            if (string.IsNullOrEmpty(address)) return string.Empty;
            string clean = address.Replace("\\", "/").Trim();
            if (clean.StartsWith("/")) clean = clean.Substring(1);
            if (clean.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 7);
            else if (clean.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 6);
            else if (clean.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(0, clean.Length - 4);
            return clean;
        }

        /// <summary>资源路径 (Assets/...) 转地址；不在 Res / Lua 目录下返回 false。</summary>
        public static bool TryFromAssetPath(string assetPath, out string address)
        {
            address = null;
            if (string.IsNullOrEmpty(assetPath)) return false;
            string path = assetPath.Replace("\\", "/");
            if (path.StartsWith(ResRoot, StringComparison.Ordinal))
            {
                address = StripExtension(path.Substring(ResRoot.Length));
            }
            else if (path.StartsWith(LuaRoot, StringComparison.Ordinal))
            {
                address = LuaPrefix + StripExtension(path.Substring(LuaRoot.Length));
            }
            return address != null;
        }

        private static string StripExtension(string relative)
        {
            int slash = relative.LastIndexOf('/');
            int dot = relative.LastIndexOf('.');
            return dot > slash ? relative.Substring(0, dot) : relative;
        }
    }
}
