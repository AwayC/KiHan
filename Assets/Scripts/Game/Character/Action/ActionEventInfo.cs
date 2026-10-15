using System;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作事件类型
    /// - Spawn / Buff 影响逻辑：生成投射物 / 分身 (BattleWorld 在本帧末尾统一执行)；给自己添加一层 Buff
    /// - Effect / Camera 只通知表现层 (BattleWorld.ViewEvents)，不影响逻辑状态
    /// - BuffRemoved 只出现在表现事件中 (Buff 到期)
    /// </summary>
    public enum ActionEventType
    {
        Effect = 0,
        Spawn = 1,
        Camera = 2,
        Buff = 3,
        BuffRemoved = 4,
    }

    /// <summary>
    /// 动作时间轴上的事件 (运行时)
    /// 触发规则与判定盒一致：本逻辑帧处理过的动画帧 [TickFromFrame, TickToFrame) 覆盖 frame 时触发一次；
    /// 从中途帧切入动作时，之前帧的事件不补发；循环动作每轮重新触发
    /// </summary>
    [Serializable]
    public struct ActionEventInfo
    {
        public int frame;
        public ActionEventType type;

        /// <summary>
        /// Effect：特效资源地址；Camera：镜头效果名 (见 CameraEffects)；Buff：buff id (buffs.json)
        /// </summary>
        public string asset;

        /// <summary>
        /// Spawn：生成物 id (spawns.json)
        /// </summary>
        public string spawnId;

        /// <summary>
        /// 帧库锚点名称：为空时以脚底为原点；锚点在事件帧不存在时同样以脚底为原点
        /// </summary>
        public string anchor;

        /// <summary>
        /// 相对锚点的整数像素偏移 (x 前方，y 高度，随朝向镜像)
        /// </summary>
        public Vector2Int offset;

        /// <summary>
        /// Effect：挂到角色身上跟随移动 (指定锚点时逐帧跟随锚点)
        /// </summary>
        public bool bind;

        /// <summary>
        /// Effect：角色切换到其他动作时立即停止 (如被打断时收回螺旋丸)
        /// </summary>
        public bool stopOnActionEnd;

        /// <summary>
        /// Effect：循环播放，不自动回收 (必须配合 stopOnActionEnd)
        /// </summary>
        public bool loop;
    }

    /// <summary>
    /// 动作事件配置 DTO (类型采用名称字符串，如 "Effect")
    /// </summary>
    [Serializable]
    public struct ActionEventData
    {
        public int frame;

        [Tooltip("Effect / Spawn / Camera / Buff")]
        public string type;

        public string asset;
        public string spawnId;
        public string anchor;
        public Vector2Int offset;
        public bool bind;
        public bool stopOnActionEnd;
        public bool loop;

        public ActionEventInfo ToRuntime()
        {
            return new ActionEventInfo
            {
                frame = frame,
                type = ActionData.ParseEnum(type, ActionEventType.Effect),
                asset = asset ?? string.Empty,
                spawnId = spawnId ?? string.Empty,
                anchor = anchor ?? string.Empty,
                offset = offset,
                bind = bind,
                stopOnActionEnd = stopOnActionEnd || loop,
                loop = loop
            };
        }
    }

    /// <summary>
    /// 镜头效果名 (Camera 事件的 asset)
    /// </summary>
    public static class CameraEffects
    {
        public const string Impact = "impact";
        public const string ImpactHeavy = "impact_heavy";
    }

    /// <summary>
    /// 逻辑 -> 表现层的动作事件通知 (纯数据，每帧清空，表现层逐帧消费)
    /// </summary>
    public struct ViewEvent
    {
        public ActionEventType Type;
        public int EntityId;

        /// <summary>
        /// 触发时角色的动作序号 (ActionController.ActionSerial)，表现层据此判断动作是否已结束
        /// </summary>
        public int ActionSerial;

        public string Asset;
        public string Anchor;
        public Vector2Int Offset;
        public bool Bind;
        public bool StopOnActionEnd;
        public bool Loop;
        public bool FacingLeft;

        /// <summary>
        /// Buff / BuffRemoved：角色内的 Buff 层编号
        /// </summary>
        public int BuffSerial;

        /// <summary>
        /// 触发时锚点 (含偏移) 的逻辑坐标 (x 横向, y 纵深, z 高度)，非跟随特效使用
        /// </summary>
        public FPVector3 Position;
    }
}
