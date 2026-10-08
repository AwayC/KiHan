# KiHan 格斗框架与底层架构全量重构规划方案 (REFACTOR_PLAN)

> **文档定位**：本项目（KiHan）从早期“硬编码状态机 + 纯 2D 平铺层级 + 浮点物理”向工业级“**定点数确定性逻辑内核 + ACT 数据驱动动作系统 + 真实 3D 深度渲染 + 统一资源管线 + 可视化动作编辑器**”全量重构的技术路线与执行规范。供方案审核与迭代编辑。

---

## 目录
- [一、 重构核心目标与设计原则](#一-重构核心目标与设计原则)
- [二、 架构演进：现状 vs 目标架构](#二-架构演进现状-vs-目标架构)
- [三、 核心子系统详细改造方案](#三-核心子系统详细改造方案)
  - [1. 定点数数学库与确定性动力学](#1-定点数数学库与确定性动力学)
  - [2. 真实 3D 纵深渲染与 2D/3D 混合深度穿插](#2-真实-3d-纵深渲染与-2d3d-混合深度穿插)
  - [3. 统一资源管理与分级对象池体系](#3-统一资源管理与分级对象池体系)
  - [4. ACT 数据驱动动作框架与确定性输入流](#4-act-数据驱动动作框架与确定性输入流)
  - [5. 5 维打击反馈、霸体与浮空/扫地/站立保护机制](#5-5-维打击反馈霸体与浮空扫地站立保护机制)
  - [6. 技能资源系统、查克拉与分身生成体系](#6-技能资源系统查克拉与分身生成体系)
  - [7. 动画表现驱动解耦与骨骼挂点系统](#7-动画表现驱动解耦与骨骼挂点系统)
  - [8. ACT 可视化动作时间轴编辑器](#8-act-可视化动作时间轴编辑器)
- [四、 重构里程碑与任务清单 (Milestones 1 ~ 8)](#四-重构里程碑与任务清单-milestones-1--8)
- [五、 鸣人 (Naruto) 动作规格设计表 (参照 naruto.md)](#五-鸣人-naruto-动作规格设计表-参照-narutomd)
- [六、 质量验收、Desync 自动化校验与风险策略](#六-质量验收desync-自动化校验与风险策略)

---

## 一、 重构核心目标与设计原则

1. **绝对的帧同步确定性 (Deterministic Lockstep)**：
   - 逻辑层与表现层 100% 隔离。逻辑层严禁出现 `Time.deltaTime`、`UnityEngine.Random`、IEEE 754 浮点数、Unity 原生 Collider / Rigidbody。
   - 所有物理位移、判定盒、技能 CD、动作流转完全基于定点数（`FP`）与固定逻辑帧（Fixed Tick）。
2. **数据驱动动作系统 (Data-Driven ACT Engine)**：
   - 废弃针对每个角色手写 C# State 类的传统状态机模式。
   - 动作、连招打断（`CancelTag` / `BeCancelledTag`）、搓招指令（`ActionCommand`）、攻击盒（`AttackInfo`）、受击动作改变（`ActionChangeInfo`）全部由配置文件驱动。
3. **彻底解决 2.5D 与 3D 素材穿插问题**：
   - 摒弃脆弱的手算 `sortingOrder` 机制，直接将纵深映射为 Unity 世界坐标的真实 Z 轴，依托 GPU 硬件级深度缓冲（Z-Buffer）实现 2D 贴花、3D 粒子、3D 角色网格的自然遮挡。
4. **战斗中“0 IO，0 动态分配 (Zero-Alloc & Zero-IO)”**：
   - 战斗局内严禁动态调用 `Resources.Load` 或无保护的 `Instantiate`。
   - 建立统一抽象的 `IAssetProvider` 与分级生命周期对象池，对局前完成预热，对局中仅做池内复用。
5. **所见即所得的编辑器工具链**：
   - 提供可视化时间轴编辑器（Action Timeline Editor），打通动画切片、判定盒绘制、取消窗口配置与事件标记的全流程。

---

## 二、 架构演进：现状 vs 目标架构

### 1. 架构对比总览

```
【现状 As-Is】
客户端输入 -> InputFrame
               ↓
        LSRoom.Tick() (固定帧)
               ↓
    CharacterEntity (浮点 Vector2 + height 混合)
               ↓
   硬编码状态机 StateMachine (CommonStateIdle, NarutoStateAttack...)
               ↓ (直接耦合美术路径)
   Resources.Load<AnimationFrameData>()
               ↓
   PlayerView -> 手动计算 sortingOrder = (2000 - y*100)*2
               (导致 3D 粒子特效、网格层级错乱穿插)

────────────────────────────────────────────────────────────────────────

【目标 To-Be】
网络输入流 -> InputFrame (6 Bytes)
                    ↓
       DeterministicCommandSource (离散帧按键队列与搓招识别)
                    ↓
       ActionController (ACT 动作控制器: CancelTag 决策 / 逻辑 Tick 推进)
                    │
  ┌─────────────────┼──────────────────────────────┐
  │                 │                              │
[定点数动力学]     [5维受击与保护]               [技能与生成工厂]
ForceMove / 重力   平推/击飞/吸附/抓取/扫地       SimpleEntity vs Actor 分身
  │                 │                              │
  └─────────────────┼──────────────────────────────┘
                    ↓
         生成逻辑快照 Snapshot 与 ActionEvent 队列 (纯数据)
══════════════════════════════════════════════════════════════════ [逻辑/表现严格分水岭]
                    ↓ (单向通知)
         EntityView 表现层 (LateUpdate 插值平滑)
                    ├─ Transform 映射: X->X, Height->Y, Y(纵深)->Z (真实3D深度)
                    ├─ 深度测试: Alpha-Test ZWrite Shader (GPU 硬件裁剪)
                    ├─ 动画驱动: IAnimationDriver (2D 序列帧 / 3D Animator)
                    └─ 资源管理: AssetManager + EnhancedObjectPool (全池化)
```

---

## 三、 核心子系统详细改造方案

### 1. 定点数数学库与确定性动力学
- **定点数方案选择**：
  - 引入 64 位定点数（Q32.32 精度）基础库 `FP`，整数部分 32 位，小数部分 32 位，精度达 $2.32 \times 10^{-10}$，满足跨平台绝对一致性。
  - 数据结构：`FP`、`FPVector2`、`FPVector3`、`FPMath`（含查表法的高性能确定性 `Sin`, `Cos`, `Atan2`, `Sqrt`）。
- **实体物理字段重构 (`LogicEntity.cs`)**：
  ```csharp
  public abstract class LogicEntity
  {
      public int EntityId;
      public FPVector2 pos;       // 水平二维坐标 (X: 左右横向, Y: 决斗场纵深前后)
      public FP height;           // 离地真实高度
      public FPVector2 velocity;  // 水平速度
      public FP h_vel;            // 垂直起跳/受击速度
      public FP Gravity;          // 动态重力加速度
      public bool IsAirborne => height > FP.Zero || h_vel > FP.Zero;
  }
  ```
- **表现层转换器规范**：
  - 逻辑层禁止引用 `UnityEngine.Vector3`，表现层仅在渲染更新时单向读取 `pos.ToVector3()`。

### 2. 真实 3D 纵深渲染与 2D/3D 混合深度穿插
- **坐标系映射公式**：
  $$\text{World Position} = \left( pos.x.\text{ToFloat}(),\, height.\text{ToFloat}(),\, pos.y.\text{ToFloat}() \right)$$
  - 左右走位对应世界坐标 $X$。
  - 跳跃与浮空对应世界坐标 $Y$（真实高度）。
  - 决斗场上下跑动对应世界坐标 $Z$（真实纵深）。
- **摄像机视角标定**：
  - 采用**正交倾角相机 (Orthographic Camera)**：
    - `Rotation = (18°, 0°, 0°)` 俯视决斗场。
    - 优势：完全消除广角透视引起的 2D 像素角色近大远小变形，同时为 3D 空间提供自然的前后视深。
- **2D/3D 深度穿插 Shader 规范**：
  - 角色 2D 材质采用带 `ZWrite On` 的 Alpha-Test Shader（Cutoff = 0.1f）。
  - 角色在 3D 场景中等价于带深度写入的立式广告牌，3D 特效（如螺旋丸、雷切网格粒子）穿过身前或身后时，由 GPU 深度缓冲区自动裁切，无需手动计算 sortingOrder。
- **地图与阴影**：
  - 地表平铺于 $Y = 0$ 平面（水平 X-Z 面）。
  - 脚下阴影 Quad 锚定在 $Y = 0.01$，坐标恒为 $(pos.x, 0.01, pos.y)$，随跳跃高度仅缩放透明度与大小。

### 3. 统一资源管理与分级对象池体系
- **资源路径解耦与提供器模式 (`IAssetProvider`)**：
  ```csharp
  public interface IAssetProvider
  {
      T LoadAsset<T>(string address) where T : UnityEngine.Object;
      UniTask<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object;
      void ReleaseAsset(string address);
  }
  ```
  - 初期实现：`ResourcesAssetProvider`（平滑兼容本地工程）。
  - 远期实现：`AddressablesAssetProvider`（无需修改上层业务即可开启热更分包）。
- **分级生命周期对象池 (`EnhancedObjectPool`)**：
  - 契约接口：`IPoolable`（`OnSpawn()` 初始化粒子状态；`OnRecycle()` 还原速度、停止音频）。
  - 对象池分类隔离：
    1. **特效池 (`FXPool`)**：打击火花（`Spark1`~`Spark5`）、刀光贴花、3D 粒子，绑定自动回收组件。
    2. **实体表现池 (`EntityViewPool`)**：火球、苦无等简单投射物预制体；影分身预制体。
    3. **UI 元素池 (`UIPool`)**：伤害跳字文本（`DamageTextNode`）、血条飘字。
- **局内加载管线**：
  - 选人结算完毕进入对局 Loading：解析双方角色 Action 配置表引用的全部资源，预先实例化灌入对象池（Prewarm），战斗期间保持 0 磁盘 IO 与 0 GC 发生。

### 4. ACT 数据驱动动作框架与确定性输入流
- **动作数据结构 (`ActionInfo`) 字段规范**：
  - `id` (string)：动作唯一标识符。
  - `animKey` (string)：表现层关联动画名。
  - `catalog` (string)：动作类别（如 `Hurt_Ground`, `Hurt_Air`, `Attack`）。
  - `durationTicks` (int)：动作持续的固定逻辑帧数。
  - `autoNextActionId` (string)：自然播放结束后的流向动作。
  - `keepPlayingAnim` (bool)：循环或打断自身时是否维持当前播放进度。
  - `autoTerminate` (bool)：未持续接收到输入时是否提前终止。
  - `cancelTag` (CancelTag[])：该动作拥有的打断标签。
  - `beCancelledTag` (BeCancelledTag[])：可被其他打断标签中断的逻辑帧窗口及优先级。
  - `inputAcceptance` (MoveInputAcceptance[])：各帧段移动摇杆速度继承比率。
  - `attacks` (AttackInfo[])：包含的攻击段落数值与 5 维反馈。
- **确定性指令缓冲源 (`IActionCommandSource`)**：
  - 帧同步收到的 `InputFrame` 经由此层转化为离散的 `KeyMap` 序列（Punch, Kick, Skill1, Skill2, 方向 1~9 宫格）。
  - 维护固定容量的滑动逻辑帧队列，支持按键有效帧窗口校验（`validInTicks`）与经典格斗搓招识别。
- **位移计算模型 (`ForceMove`)**：
  - 动作位移分为常规移动（`NatureMove` = 摇杆输入 $\times$ `MoveInputAcceptance` + `RootMotion`）与强制外力位移（`ForceMove`）。
  - 处于受击推移、技能突进时，`ForceMove` 接管角色位移，采用确定性定点数插值曲线（Linear, QuadOut 等）。

### 5. 5 维打击反馈、霸体与浮空/扫地/站立保护机制
- **5 维打击物理与判定流转**：
  1. **平推 (Push)**：受击者保持地面受击状态（`hurt_1`, `hurt_2`），施加水平 `ForceMove` 位移；若受击者已在空中，自动转换为击飞。
  2. **击飞 (ToAir / Launch)**：赋予垂直初速度 `h_vel`，受击者强制切换至 `hurt_toair`，在重力作用下经历滞空、掉落（`hurt_fall`）、落地（`hurt_land`）。
  3. **吸附 (Suction)**：以技能核心点为中心，每帧向受击者施加定点数向心拉扯位移。
  4. **抓取 (Grab)**：
     - 判定生效时，受击者被强制锁定为专用抓取受击动作，坐标硬性吸附于释放者的抓取锚点。
     - 抓取属于强控制，无视普通霸体；若释放者在抓取过程中被第三方打断，受击者绑定立即解除并转为倒地。
  5. **扫地 (OTG - Off The Ground)**：
     - 检测受击者是否处于倒地状态（`lie` / `fall`）。仅当招式携带 `canHitLying = true` 时才能将倒地目标二次挑起。
- **霸体等级裁决体系 (`ArmorLevel`)**：
  - 等级顺序：`Normal(0) < Skill(1) < Super(2) < KingKong(3) < Invincible(4)`。
  - 当受击发生时，若攻击方的破霸等级低于受击者当前的 `ArmorLevel`，受击者仅结算伤害与火花，不触发受击动作、不打断当前招式、不进入硬直。
- **火影手游级保护体系 (Juggle & Stun Protection)**：
  - **黄保阶段**：受击者离地后累计承受伤害达到阈值（如最大生命值 15%），受击硬直减少 30%，身体受击轮廓变黄。
  - **红保阶段**：累计伤害达到高危阈值（如最大生命值 28%），受击者重力加速度增加 150%（急速下坠），受击推力衰减，强制终止浮空无限连。
  - **扫地保护**：倒地遭受的第一段扫地伤害计入保护池，倒地受击伤害超过上限（如最大生命值 20%），触发保护机制，受击者闪白并无敌翻滚起身。
  - **站立保护**：连续受到地面平推时，硬直累计达到上限强制倒地，防止地面无限连推。

### 6. 技能资源系统、查克拉与分身生成体系
- **技能资源控制器 (`SkillController`)**：
  - 管理技能 CD（逻辑帧单位倒计时）、技能段落流转（多段技能分支与超时还原）。
  - 查克拉（奥义点）体系：最大 4 颗奥义点，命中敌人增加奥义点，释放奥义消耗 4 点，释放替身术消耗 1 点并触发无敌替身木头。
- **分身与投射物生成规范 (严格遵从 `EntityController.md`)**：
  - 生成类型严格划分为两类：
    - `SpawnObjectType.SimpleEntity`：火球、手里剑、风魔手里剑、地刺陷阱。使用轻量级生命周期，不挂载动作状态机。
    - `SpawnObjectType.Actor`：鸣人影分身、木叶丸、佐助雷分身、通灵召唤兽。复用 `CharacterEntity` + `ActionController`，由 `CloneBrain` 注入输入指令，具备完整的动作打断与受击能力。
  - 分身生命周期组件 (`SpawnedActorLifecycle`)：追踪主人归属、存活逻辑帧倒计时、消失动作调用与对象池回收。

### 7. 动画表现驱动解耦与骨骼挂点系统
- **确定性动画控制器抽象 (`IAnimationDriver`)**：
  - 逻辑层只输出动作 ID、当前逻辑帧索引、归一化进度百分比。
  - 表现层按需装配：
    - `Sprite2DAnimationDriver`：解析 2D 序列帧图集进行 Sprite 切换，支持残影生成与图层贴花。
    - `Animator3DDriver`：为后续升级 3D 角色预留，直接根据 ActionId 执行 `CrossFadeInFixedTime`。
- **骨骼与锚点挂接系统 (`SocketAnchor`)**：
  - 统一定义标准骨骼挂点：`Socket_Head`、`Socket_Chest`、`Socket_Hand_L`、`Socket_Hand_R`、`Socket_Root`。
  - 技能光效、武器、受击火花、抓取依附严格绑定至对应挂点，并随角色面向翻转保持对称。

### 8. ACT 可视化动作时间轴编辑器 (Action Timeline Editor)
- **编辑器界面核心布局**：
  - **多轨道时间轴 (Multi-Track Timeline)**：
    - **帧序列轨 (Frame Track)**：直观拖拽帧长度（Ticks）。
    - **攻击盒轨 (HitBox Track)**：可视化色块展示攻击阶段，点击配置伤害与 5 维参数。
    - **受击盒轨 (HurtBox Track)**：展示受击判定盒激活区间。
    - **取消窗轨 (Cancel Window Track)**：高亮展示当前动作允许被 Cancel 的逻辑帧范围与 Tag。
    - **事件轨 (Event Track)**：在指定帧打点标记音效、火花特效、震屏与生成物。
    - **位移轨 (RootMotion Track)**：绘制定点数位移曲线。
- **Scene 视口交互**：
  - 鼠标直接在 Scene 视口中拖拽调整判定盒尺寸与相对偏移。
  - 支持逐逻辑帧前进/后退预览、连招打断模拟预览。
- **一键导出验证**：
  - 自动运行静态校验（死循环检测、无效打断标签预警），一键生成运行时的 JSON/二进制动作资产。

---

## 四、 重构里程碑与任务清单 (Milestones 1 ~ 8)

```mermaid
gantt
    title KiHan 架构全量重构排期路线
    dateFormat  YYYY-MM-DD
    section 基础设施
    M1: 定点数数学库与动力学基础         :m1, 2026-10-01, 7d
    M2: 资源管理体系与分级对象池         :m2, after m1, 5d
    M3: 真实3D深度渲染与层级穿插         :m3, after m1, 6d
    section 动作内核
    M4: ACT 动作框架与确定性输入流       :m4, after m2, 8d
    M5: 5维受击反馈与保护机制           :m5, after m4, 8d
    M6: 技能资源/Buff/分身生成系统      :m6, after m5, 6d
    section 工具与落地
    M7: ACT 可视化动作时间轴编辑器      :m7, after m4, 10d
    M8: 鸣人角色全量迁移与联机验收       :m8, after m6, 7d
```

| 里程碑 | 核心产出文件 | 重点改造内容 | 验收标准 |
| :--- | :--- | :--- | :--- |
| **M1: 定点数数学库与物理改造** | `Math/FP.cs`<br>`Math/FPVector2.cs`<br>`Core/LogicEntity.cs`<br>`LogicCommon.cs` | 1. 建立纯定点数数学运算库<br>2. 实体坐标、速度、高度、重力定点数化<br>3. 判定盒 `LogicBox` 切换为定点数碰撞 | 编写纯单元测试：跨平台运行 10000 次复杂物理模拟运算，哈希值绝对一致。 |
| **M2: 资源管理与对象池** | `Managers/Asset/IAssetProvider.cs`<br>`Managers/Asset/ObjectPoolManager.cs`<br>`View/Component/AutoRecycleFX.cs` | 1. 抽象资源提供器接口<br>2. 实现按类型隔离的分级对象池<br>3. 改造战斗前加载预热流程 | 连续对战 5 分钟，战斗运行时无任何 `Resources.Load` 与新实例分配产生的 GC Spike。 |
| **M3: 3D 纵深渲染与穿插** | `View/EntityView.cs`<br>`View/Rendering/ZWriteSprite.shader`<br>`View/Component/SocketAnchor.cs` | 1. `EntityView` 坐标映射为 X->X, Height->Y, Y->Z<br>2. 摄像机调整为 18° 正交倾角<br>3. 角色材质开启 Alpha-Test ZWrite | 3D 粒子特效在角色身前身后穿梭时产生自然的前后遮挡，彻底移除手算 sortingOrder。 |
| **M4: ACT 动作核心与输入** | `Characters/Action/ActionInfo.cs`<br>`ActionController.cs`<br>`DeterministicCommandSource.cs`<br>`ForceMove.cs` | 1. 动作配置数据结构定点数化<br>2. 逻辑帧驱动的 `ActionController`<br>3. 搓招按键队列与打断窗口决策 | 动作可由 JSON 表驱动，普攻 1~4 段可通过 `CancelTag` 流畅打断与自然衔接。 |
| **M5: 5 维打击与保护体系** | `Characters/Combat/HitSystem.cs`<br>`JuggleProtectionTracker.cs`<br>`ArmorLevel.cs`<br>`HitRecord.cs` | 1. 落地平推、击飞、吸附、抓取、扫地 5 种受击物理<br>2. 霸体等级判定<br>3. 浮空黄保、红保加重与扫地强制保护起身 | 浮空受击累计达到阈值时光速下坠断连；扫地伤害超标后闪白无敌起身。 |
| **M6: 技能/查克拉/分身系统** | `Characters/Combat/SkillController.cs`<br>`SpawnFactory.cs`<br>`SpawnedActorLifecycle.cs`<br>`CloneBrain.cs` | 1. 多段技能 CD 与查克拉奥义点管理<br>2. 区分 SimpleEntity 与 Actor 分身生成<br>3. 分身通过 `CloneBrain` 驱动动作控制器 | 鸣人可释放多段技能；分身生成后独立执行攻击动作并按存活时限回收。 |
| **M7: 可视化时间轴编辑器** | `Editor/ActionTimeline/ActionEditorWindow.cs`<br>`TimelineTracks.cs`<br>`BoxGizmoDrawer.cs` | 1. 实现时间轴多轨道可视化（帧/盒/打断/事件/位移）<br>2. Scene 视口鼠标直观拖拽编辑判定盒<br>3. 动作数据完整性校验与导出 | 策划或开发者无需手写 JSON，在编辑器内 10 分钟内完成一个全新动作的完整配表。 |
| **M8: 鸣人迁移与联机验收** | `ArtAssets/Characters/90001_Naruto/`<br>`naruto_actions.json`<br>`NarutoEntity.cs` | 1. 迁移鸣人全部动作至新框架<br>2. 彻底移除旧版状态机代码<br>3. 双端联机帧同步对战压力测试 | 双端 KCP 联机对战，连续执行 1000 帧复杂连招对抗，状态校验和一致，0 脱同步。 |

---

## 五、 鸣人 (Naruto) 动作规格设计表 (参照 naruto.md)

以下为重构后鸣人核心动作集合规范，直接映射为 `naruto_actions.json`：

| 动作 ID (`id`) | 动作类型 (`catalog`) | 自身打断标签 (`cancelTag`) | 允许被取消窗口 (`beCancelledTag`) | 自然流向 (`autoNextActionId`) | 核心特性 / 事件说明 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`idle`** | `Base` | - | `move`, `attack1`, `skill1`, `skill2` | `idle` | 呼吸待机，允许全招式打断，`keepPlayingAnim=true` |
| **`move`** | `Base` | `move` | `move`, `attack1`, `skill1`, `skill2` | `idle` | 跑步，继承摇杆移动速度 (`inputAcceptance=1.0`) |
| **`attack1`** | `Attack` | `attack1`, `attack` | 50%~100% 允许 `attack2`, `skill1`, `skill2` | `idle` | 平推受击判定，造成轻微地面推移 |
| **`attack2`** | `Attack` | `attack2`, `attack` | 45%~100% 允许 `attack3`, `skill1`, `skill2` | `idle` | 平推受击判定，带前冲 RootMotion |
| **`attack3`** | `Attack` | `attack3`, `attack` | 50%~100% 允许 `attack4`, `skill1`, `skill2` | `idle` | 击退判定，使目标进入较大受击硬直 |
| **`attack4`** | `Attack` | `attack4`, `attack` | 60%~100% 允许 `skill1`, `skill2` | `idle` | 击飞判定（`ToAir`），目标升空并进入浮空保护计算 |
| **`skill1`** | `Skill` | `skill`, `skill1` | 后摇阶段允许 `beCancelTag` | `idle` | 突进分身踢击，霸体等级为 `Skill` |
| **`skill2_1`** | `Skill` | `skill`, `skill2_1` | 允许派生 `skill2_2` | `skill2_2` | 第 10 帧触发事件 `spawnEntity: CharClone` 召唤分身协同 |
| **`skill2_2`** | `Skill` | `skill`, `skill2_2` | 后摇阶段允许取消 | `idle` | 螺旋丸本体突进多段判定，最后一击重度击飞 |
| **`hurt_1`** | `Hurt` | - | 无（硬直结束后解脱） | `idle` | 地面轻受击硬直 |
| **`hurt_2`** | `Hurt` | - | 无 | `idle` | 地面重受击硬直 |
| **`hurt_toair`**| `Hurt` | - | 无 | `hurt_inair` | 浮空上升阶段 |
| **`hurt_inair`**| `Hurt` | - | 无 | `hurt_fall` | 浮空最高点下落阶段 |
| **`hurt_fall`** | `Hurt` | - | 无 | `hurt_land` | 坠落阶段（可被扫地技能挑起） |
| **`hurt_land`** | `Hurt` | - | 无 | `lie` | 倒地触地弹起阶段 |
| **`lie`** | `Lie` | - | 达到保护阈值时允许触发闪白起身 | `standup` | 倒地不起状态，仅受扫地技能影响 |
| **`standup`** | `Standup`| - | 起身无敌帧 | `idle` | 起身阶段，附带 `Invincible` 霸体 |

---

## 六、 质量验收、Desync 自动化校验与风险策略

### 1. 帧同步脱同步 (Desync) 自动化校验
- **状态哈希校验管线**：
  - 每 30 逻辑帧提取对局上下文计算哈希：
    $$\text{FrameChecksum} = \text{CRC32}\left( \text{FrameId},\, \sum_{\text{entities}} (\text{EntityId} \oplus pos.x \oplus pos.y \oplus height \oplus Blood \oplus ActionId) \right)$$
  - 联机对战中，客户端在上传自己下一帧输入的同时携带最近历史帧的 Checksum，网关/服务器比对不同客户端的 Checksum；一旦分歧立即录制前 60 帧输入包并触发警报，便于精准复现修复。

### 2. 核心技术风险与对应预案
1. **风险：2D Sprite 开启 ZWrite 后产生透明边缘黑边/穿插瑕疵**
   - **预案**：使用 Alpha Testing (Cutoff) 时，图集边缘留出 2px 扩张透明边；对半透明特效（羽化火花）独立走半透明渲染队列并关闭 ZWrite。
2. **风险：定点数与 Unity 浮点渲染插值抖动**
   - **预案**：表现层使用时间累加插值算法（`Time.deltaTime / LOGIC_TICK_TIME`），保证当逻辑帧率为 30 FPS 时，表现层在 120Hz/144Hz 刷新率下依然丝滑平滑过渡。
3. **风险：重构周期长导致主干功能长期处于不可运行状态**
   - **预案**：采用**双内核并行走**策略。先在独立目录开发定点数与 ACT 内核（Milestone 1~4），通过单机测试场景验证无误后，再接入 `BattleManager` 替换老旧状态机，保障项目主工程始终随时可运行体验。

---
*本方案文档保存在项目根目录下，方便随时查阅与审阅批注。*
