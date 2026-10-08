namespace KiHan.Asset
{
    /// <summary>
    /// 对象池生命周期感知契约接口
    /// 放入对象池的预制体或其子节点可实现此接口，在出池与入池时接收通知
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 从对象池取出唤醒时调用 (重置粒子、启动计时器、重置动画)
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// 回收归还对象池时调用 (停止音效、清除物理速度、复位参数)
        /// </summary>
        void OnRecycle();
    }
}
