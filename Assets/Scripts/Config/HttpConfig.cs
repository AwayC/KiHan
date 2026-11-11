namespace KiHan.Config
{
    public static class HttpConfig
    {
        public const string LoginServerUrl = "http://localhost:8080";
        public const string AppVersion = "1.0.0";

        /// <summary>热更资源服务器 (静态目录，即 Build/Bundles)：/&lt;平台&gt;/version.json、manifest_N.json、包文件</summary>
        public const string HotUpdateUrl = "http://127.0.0.1:8088";
    }
}
