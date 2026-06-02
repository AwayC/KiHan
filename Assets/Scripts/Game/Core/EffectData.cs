using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 逻辑层发送给表现层的特效播放指令数据
    /// </summary>
    public struct EffectData
    {
        public int EffectId;            // 用于取消或追踪特效的唯一标识
        public string EffectName;       // 预制体资源名称 (在 Resources/Effects/ 下)
        public string AnchorName;       // 可选：绑定的特效锚点名称（需要BindEntity不为空）
        public Vector2 WorldPos;        // 触发时的基础世界坐标
        public int Height;              // 触发时的高度
        public Vector2 Offset;          // 相对偏移量
        public bool IsFacingLeft;       // 是否朝左（影响 Scale X）
        public LogicEntity BindEntity;  // 若不为空，表现层会自动把特效设为该实体对应 View 的子物体，实现严丝合缝的跟随
    }
}
