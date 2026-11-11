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
}
