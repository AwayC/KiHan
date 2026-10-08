using UnityEngine;

namespace KiHan.Asset
{
    public enum PoolCategory
    {
        Default,
        FX,       // 打击火花、刀光贴花、粒子
        Entity,   // 技能派生体、投射物、分身
        UI,       // 伤害跳字、HUD飘字
        Audio,    // 动态音效节点
    }

    /// <summary>
    /// 挂载在池化 GameObject 上的元数据追踪器
    /// 使对象在调用 Recycle(gameObject) 时无需传回 address 即可自动识别所属池子
    /// </summary>
    [DisallowMultipleComponent]
    public class PoolItemTracker : MonoBehaviour
    {
        public string PoolAddress { get; set; }
        public PoolCategory Category { get; set; } = PoolCategory.Default;
        public bool IsPooled { get; set; } = false;
    }
}
