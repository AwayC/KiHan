using UnityEngine;

namespace KiHan.View.Render
{
    /// <summary>
    /// 特效播放请求 (表现层内部数据，由逻辑事件如 HitEvent 转换而来)
    /// </summary>
    public struct EffectData
    {
        /// <summary>
        /// 资源地址 (Resources 相对路径) 或 EffectManager 预加载时登记的别名
        /// </summary>
        public string EffectName;

        /// <summary>
        /// 世界坐标 (已经过 BattleViewSpace 映射)；BindView 不为空时忽略
        /// </summary>
        public Vector3 WorldPos;

        /// <summary>
        /// 屏幕平面内的像素偏移 (面朝右为正，按朝向镜像)
        /// </summary>
        public Vector2 Offset;

        /// <summary>
        /// 是否朝左 (特效整体水平镜像)
        /// </summary>
        public bool IsFacingLeft;

        /// <summary>
        /// 不为空时挂到该角色的 Body 节点上，跟随角色位移与高度
        /// </summary>
        public ActorView BindView;
    }
}
