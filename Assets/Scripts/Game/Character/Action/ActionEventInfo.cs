using System;
using TrueSync;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作事件类型
    /// - Spawn 影响逻辑：生成投射物 / 分身 (BattleWorld 在本帧末尾统一执行)
    /// - Effect / Camera 只通知表现层 (BattleWorld.ViewEvents)，不影响逻辑状态
    /// - Sound / Cinematic 只通知表现层
    /// - Buff / BuffRemoved 只出现在表现事件中 (Buff 添加 / 移除时播放 / 停止循环特效)；给角色加 Buff 用动作指令 addBuff
    /// </summary>
    public enum ActionEventType
    {
        Effect = 0,
        Spawn = 1,
        Camera = 2,
        Buff = 3,
        BuffRemoved = 4,
        Sound = 5,
        Cinematic = 6,
    }

    /// <summary>Cinematic 事件的观看者 (按本机槽位过滤，表现层判断)</summary>
    public enum CinematicAudience { All = 0, Self = 1, Others = 2 }

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
        /// Effect：特效 key (优先在本角色包资源表中查找，找不到按全局 Resources 路径加载)；Camera：镜头效果名 (见 CameraEffects)；
        /// Sound：音效 key (SoundCue 或 AudioClip，优先本角色包)；Cinematic：演出预制体 key (根节点带 PlayableDirector + Timeline)
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
        /// Effect：挂到角色身上跟随移动 (指定锚点时逐帧跟随锚点)；为 false 时留在触发位置，不随角色移动
        /// Camera shot：镜头目标点跟随施法者移动
        /// </summary>
        public bool bind;

        /// <summary>
        /// Effect / Sound / Cinematic / Camera：角色切换到其他动作时立即停止 (如被打断时收回螺旋丸，镜头复位)
        /// </summary>
        public bool stopOnActionEnd;

        /// <summary>
        /// Effect：循环播放，不自动回收 (必须配合 stopOnActionEnd)
        /// </summary>
        public bool loop;

        /// <summary>
        /// Cinematic / Camera：谁能看到 (All / Self / Others)
        /// </summary>
        public CinematicAudience audience;

        /// <summary>
        /// 使用 worldPos 作为事件位置 (配置了 worldPos 任一坐标时为 true)
        /// </summary>
        public bool useWorldPos;

        /// <summary>
        /// 指定的逻辑世界坐标 (x 横向, y 纵深, z 高度；不随朝向镜像，忽略 anchor / offset，不跟随角色)
        /// Effect / Spawn / Cinematic / Camera shot 在此位置生成
        /// </summary>
        public FPVector3 worldPos;

        /// <summary>
        /// Camera：指令参数 (见 CameraEffects)
        /// </summary>
        public CameraEventInfo camera;
    }

    /// <summary>
    /// 动作事件配置 DTO (类型采用名称字符串，如 "Effect")
    /// </summary>
    [Serializable]
    public struct ActionEventData
    {
        public int frame;

        [Tooltip("Effect / Spawn / Camera / Buff / Sound / Cinematic")]
        public string type;

        public string asset;
        public string spawnId;
        public string anchor;
        public Vector2Int offset;
        public bool bind;
        public bool stopOnActionEnd;
        public bool loop;

        [Tooltip("Cinematic：All / Self / Others")]
        public string audience;

        [Tooltip("指定逻辑世界坐标生成 (x 横向, y 纵深, z 高度，字符串)：填任一坐标即生效，忽略 anchor / offset / bind")]
        public EventPositionData worldPos;

        [Tooltip("Camera：shot / shake / pulse / reset 的参数")]
        public CameraEventData camera;

        public ActionEventInfo ToRuntime()
        {
            bool world = worldPos.IsSet;
            ActionEventType parsed = ActionData.ParseEnum(type, ActionEventType.Effect);
            if (parsed == ActionEventType.Buff || parsed == ActionEventType.BuffRemoved)
            {
                Debug.LogError($"[ActionData] 动作事件不能使用 {parsed} 类型 (第 {frame} 帧)：给角色加 Buff 改用动作指令 addBuff");
            }
            return new ActionEventInfo
            {
                frame = frame,
                type = parsed,
                asset = asset ?? string.Empty,
                spawnId = spawnId ?? string.Empty,
                anchor = anchor ?? string.Empty,
                offset = offset,
                bind = bind && !world,
                stopOnActionEnd = stopOnActionEnd || loop,
                loop = loop,
                audience = ActionData.ParseEnum(audience, CinematicAudience.All),
                useWorldPos = world,
                worldPos = world ? worldPos.ToRuntime() : FPVector3.zero,
                camera = camera.ToRuntime(asset)
            };
        }
    }

    /// <summary>
    /// 事件的逻辑世界坐标 DTO (定点数字符串，缺省为 0；三项都为空表示未指定)
    /// </summary>
    [Serializable]
    public struct EventPositionData
    {
        [Tooltip("横向 (地图中心为 0)")]
        public string x;

        [Tooltip("纵深 (视觉单位，与角色位置的 y 一致)")]
        public string y;

        [Tooltip("高度 (离地)")]
        public string z;

        public bool IsSet => !string.IsNullOrEmpty(x) || !string.IsNullOrEmpty(y) || !string.IsNullOrEmpty(z);

        public FPVector3 ToRuntime() => new FPVector3(ActionData.ParseFP(x), ActionData.ParseFP(y), ActionData.ParseFP(z));
    }

    /// <summary>
    /// 逻辑 -> 表现层的动作事件通知 (纯数据，每帧清空，表现层逐帧消费)
    /// </summary>
    public struct ViewEvent
    {
        public ActionEventType Type;
        public int EntityId;

        /// <summary>
        /// 触发者的角色 ID (生成物为其主人角色)：表现层据此在对应角色包中解析 Asset key
        /// </summary>
        public int CharacterId;

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

        /// <summary>
        /// Cinematic / Camera：谁能看到
        /// </summary>
        public CinematicAudience Audience;

        /// <summary>
        /// 触发者主人的输入槽位 (生成物取主人)，Cinematic / Camera 据此按观看者过滤
        /// </summary>
        public int OwnerSlot;

        /// <summary>
        /// Camera：指令参数
        /// </summary>
        public CameraEventInfo Camera;
    }
}
