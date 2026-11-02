using System;

namespace KiHan.UI
{
    /// <summary>
    /// UI 层级 (每层一个 Canvas，按数值从下到上绘制；sortingOrder = 层 × 100)
    /// </summary>
    public enum UILayer
    {
        Scene = 0,
        Hud = 1,
        Normal = 2,
        Popup = 3,
        Toast = 4,
        Loading = 5,
        System = 6,
    }

    /// <summary>
    /// 面板元数据：资源地址 (AssetManager / Resources 路径，不含扩展名)、所在层级、关闭后是否保留实例
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIPanelAttribute : Attribute
    {
        public string Path { get; }
        public UILayer Layer { get; }

        /// <summary>
        /// 关闭后保留实例 (再次打开直接复用)；false 时关闭即销毁
        /// </summary>
        public bool Cache { get; set; } = true;

        public UIPanelAttribute(string path, UILayer layer)
        {
            Path = path;
            Layer = layer;
        }
    }
}
