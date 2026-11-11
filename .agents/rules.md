# KiHan 架构重构与开发规范约定 (Project Specification)
# 更新时间：2026-11-11

本文档记录当前项目在 ACT 格斗框架集成与帧同步（Lockstep）改造过程中的核心强制规范。所有后续开发与子模块实现均须严格遵守。

---

## 零、 Unity 编辑器操作一律优先走 CLI

1. **有 CLI 就用 CLI**：需要编辑器参与的操作 (编译、跑测试、进出 Play 模式、截图、读控制台、检查场景 / 资源 / 预制体、执行编辑器代码) 先执行 `unicli check`，连接成功就只用 `unicli` 完成，用法见 [testing.md](testing.md) 第四节。
   - 编译：`unicli exec Compile --json`；测试：`unicli exec TestRunner.RunEditMode --json`；执行代码：`unicli eval '<C#>' --json`；全部命令：`unicli commands`。
   - 以命令返回的 JSON 为准，不再读 `Editor.log` 判断编译结果。
2. **CLI 不可用时才退回旧办法** (切换窗口焦点 / Ctrl+R 触发刷新、读 `Editor.log`、临时编辑器脚本)，并在回复里说明 CLI 不可用的原因 (编辑器没开、包没装、连接失败)。
3. 临时验证脚本中的等待必须有超时，禁止无出口的循环：主线程卡死会冻结整个编辑器，只能结束进程。

---

## 一、 文件编码与换行符标准 (File Encoding & Line Endings)

1. **统一换行符标准为 `LF` (`\n`)**：
   - 彻底废除 Windows 系统的 `CRLF` (`\r\n`)，杜绝 Git 假变更与 Shader/跨平台平台解析差异。
   - 根目录下 [.gitattributes](.gitattributes) 强制所有源码、配置与元数据 (`*.cs`, `*.shader`, `*.json`, `*.meta`, `*.prefab`) 检出和提交为 `eol=lf`。
2. **统一字符编码为 `UTF-8` (无 BOM)**：
   - 严禁保存为 GBK / CP936。
   - 根目录下 [.editorconfig](.editorconfig) 约束 IDE 在保存时自动标准化为 UTF-8、LF 并自动清除行末空格。

---

## 二、 帧同步时钟与帧率标准 (Lockstep Clock & FPS)

1. **当前逻辑帧率严格锁定为 30 FPS**（与 30fps 动画素材一一对应，每个逻辑帧推进 1 个动画帧）：
   - `GameConfig.LOGIC_FPS = 30;`
   - 服务器下发逻辑帧间隔需同步为 1/30 秒（约 33.3ms）。
2. **逻辑时钟单帧步长必须使用纯定点数**：
   - 逻辑步长常数必须为 `public static readonly FP LOGIC_TICK_TIME_FP = FP.One / LOGIC_FPS;`（Q32.32 精度，约 0.033333333 秒，绝对零累积漂移）。
   - **严禁写死 `0.033f` 之类的近似浮点数**（会产生逐秒累积的逻辑时滞）。
   - 以逻辑帧数表示的默认值必须由 `LOGIC_FPS` 推导（如 `GameConfig.LOGIC_FPS * 3` 表示 3 秒），禁止写死帧数。
3. **表现层/渲染层时钟独立**：
   - 表现层（View/插值/动画）可运行于 30/60/120 FPS（`GameConfig.RENDER_FPS = 30`）。
   - 仅在表现层（`ActorView`、`EffectView`、相机、UI）允许使用 Unity 的浮点数 `Time.deltaTime` 进行平滑 Lerp 插值。
   - **逻辑内核与物理结算严禁调用任何 `Time.deltaTime`**。

4. **逻辑帧由客户端定时器驱动 (`LockstepManager`)**：
   - 客户端按真实时间以 1/30 秒步进 (累加器)，每步先上报本机输入 (目标帧号 = 当前帧 + 1 + 输入延迟)，再执行下一个**确认帧**；确认帧未到时本步等待，不预测、不回滚。
   - 服务器只把各玩家同一帧号的输入合成确认帧下发 (2005)，不驱动客户端节奏；积压的确认帧超过上限时客户端追帧。
   - 单机 / 训练场的本地服务器 `VirtualNetworkManager` 收到输入立即回送确认帧 (输入延迟 0)；联机默认输入延迟 `LockstepManager.OnlineInputDelayFrames`。
   - 输入采样与上报跟随逻辑步，不跟随渲染帧；渲染帧率变化不改变逻辑速度。

---

## 三、 定点数体系与字符串规范 (Fixed-Point & String-First)

1. **逻辑内核变量全量定点数化**：
   - **位移与速度**：`FPVector3`、`FPVector2`、`FP moveSpeedX` / `FP moveSpeedY` (横向与纵深各自独立，摇杆只有 8 方向，斜向不归一化)、`MoveInputAcceptance.rateX` / `rateY`（横向 / 纵深移动速度接受率）。
   - **重力与动力学**：`FP weight`、`_curWeight`、`MoveInfo.moveDistance`、`MoveInfo.inSec`。
   - **战斗与命中时钟**：`AttackInfo.attack`（伤害）、`AttackInfo.hitStun`（硬直）、`AttackInfo.launch` / `air`（击飞速度）、`AttackInfo.hitSameTargetDelay`、`HitRecord.Cooldown`。
   - **动作打断与预约**：`ActionCommand.validInSec`、`ActionChangeInfo.freezingAfterChange`、`PreorderActionInfo.FreezingAfterChangeAction`。
2. **代码与配置初始化一律使用“纯字符串” (String-First Standard)**：
   - **核心原则**：不同 CPU 硬件（x86 vs ARM64）对浮点字面量（如 `0.15f`）计算存在 IEEE 754 硬件舍入差异，为保证 100% 确定性，**代码默认值和配置文件中的定点数必须声明为字符串**。
   - **纯整型解析器**：底层使用 `FP.Parse(string)` 与 `FP.TryParse`，以整数位移与除法直接生成 Q32.32 二进制位，**彻底绕开所有 FPU 浮点硬件指令**。
   - **代码编写规范示例**：
     ```csharp
     public FP weight = FP.Parse("4.5");
     public FP moveSpeedX = FP.Parse("5.0");
     public ActionCommand command = new ActionCommand(key, "0.15");
     public FPVector3 offset = new FPVector3("0.5", "0.0", "0.0");
     ```

---

## 四、 配置读取与数据分层管道 (Config DTO Architecture)

1. **分离配置 DTO 层与运行时逻辑层**：
   - **配置 DTO 层 (`ActionData.cs`)**：用于 JsonUtility 反序列化，接收原生 JSON 字段，关键定点数字段全量声明为 `string`。
   - **逻辑内核层 (`ActionInfo.cs`)**：纯确定性 `FP` / `FPVector3` 结构体。
   - **转换管道 (`ActionData.ToRuntime()`)**：集中调用 `ActionData.ParseFP`，统一将字符串配置转换为逻辑内核结构，零运行时 GC，零浮点污染。
2. **JSON 配置范例**：
   ```json
   {
     "hold": ["Down"],
     "press": ["Skill1"],
     "validInSec": "0.05",
     "rateX": "1.0",
     "hitStun": "0.12",
     "hitSameTargetDelay": "1.0",
     "push": { "x": "0.2", "y": "0", "inSec": "0.08", "tweenMethod": "Slowly" },
     "launch": { "z": "9", "x": "2" }
   }
   ```

---

## 五、 逻辑帧主循环与判定盒规范 (Tick Loop & Boxes)

1. **逻辑帧显式步进**：
   - 角色逻辑主体（`CharacterObj`）与动作控制器（`ActionController`）提供显式 `public void Tick()` / `public void Tick(FP deltaFP)`。
   - 每次步进严格消耗一个离散逻辑步长（`GameConfig.LOGIC_TICK_TIME_FP`）。
   - 瞬时位移结算统一通过 `FPVector3 ThisTickMove()` 返回，严禁直接在 Update 中修改 Transform。
2. **判定盒脱离 Unity 渲染帧**：
   - `AttackHitBox` 与 `BeHitBox` 严禁依赖 Unity 的 `Update()` 维护激活状态。
   - 统一改造为实时属性查询：
     ```csharp
     public bool Active => master != null && master.ShouldAttackBoxActive(tags);
     ```
   - 在逻辑帧碰撞冒泡判定（`CanAttackTargetNow`）时，即时获取当帧确定的开启状态。

---

## 六、 老旧系统过渡策略 (Legacy Entity Deprecation)

1. **旧内核已下线**：
   - `LogicEntity`、旧状态机与 `EntityView` / `EffectNode` 表现链路已删除，战斗统一由 `BattleWorld` + `CharacterObj` + `ActionController` 驱动，表现层由 `ActorView` 只读绑定。
   - 逻辑 -> 表现坐标映射只允许通过 `KiHan.View.Render.BattleViewSpace`。
2. **保证主干双端编译通过**：
   - 重构过程中所有中间改动必须维持 `Assembly-CSharp` 和 `Assembly-CSharp-Editor` 双端编译通过（0 警告、0 错误）。

---

## 七、 测试规范 (Testing)

1. 测试分层、用例写法、提交前检查与 Unity 验证清单见 [testing.md](testing.md)。
2. 逻辑层改动提交前必须运行 `Tests/LogicTests/run_all.sh` 并全部通过；无 Unity 环境完成的 Unity 侧改动须在提交说明中注明「未经 Unity 验证」。

---

## 八、 UI 规范 (UI)

1. 按钮交互 (统一 Q 弹按压动画)、节点绑定、布局适配、刷新性能与特殊效果的约定见 [ui.md](ui.md)。
2. 所有没有特殊设计的按钮都必须带 `UIPressBounce` 按压回弹；特殊设计的按钮挂 `UIPressBounceIgnore` 后自行实现。

---

## 九、 战斗场景与地图制作规范 (2D & 3D Map Specification)

1. **逻辑边界全局统一 (`MapLogic`)**：
   - 全部地图（无论 2D 或 3D）共用同一套纯定点数逻辑边界：`MinX = -8`、`MaxX = 8`、`MinY = -1.009371`、`MaxY = 1.009371`（`1 Unity 单位 = 1 米`）。
   - 1P 出生点固定为 `(-2, 0, 0)`，2P 出生点固定为 `(2, 0, 0)`，场地中心原点为 `(0, 0, 0)`。
   - 局内切换地图必须通过 `GameFlow.Instance.ChangeMap(MapConfig)` 触发 `LoadingPanel` 加载过渡并调用 `MapManager.Instance.ChangeScene(map)`，仅替换表现层场景实例，不改动逻辑世界状态。
2. **2D 地图与 3D 地图双模兼容 (`MapConfig.is3D` & `CameraControllor`)**：
   - **2D 地图 (`is3D = false`)**：预制体由 `MapManager` 放置在世界 `Z = BattleViewSpace.BackdropDistance (60f)`，按 `BattleViewSpace.MapScale (8/7)` 缩放，所有 `SpriteRenderer.sortingOrder` 偏移 `BackdropSortingOffset (-1000)`；主相机使用单相机正交模式 (`CameraClearFlags.SolidColor`)，分层视差由 `ParallaxEffect` 驱动。
   - **3D 地图 (`is3D = true`)**：预制体放置在世界原点 `(0, 0, 0)` 且保持 `1:1` 缩放，严禁挂载 `ParallaxEffect`，也**禁止用脚本在运行时生成场景几何**——网格、材质、贴图必须是项目资产，预制体内的层级、图层可直接编辑保存。背景由 `Map3DCameraRig` (CameraControllor 持有) 的两台透视相机绘制：
     1. **远景相机 (`BgFar3DCamera`, depth = 主相机 - 2, `SolidColor`, `Layer 9: Map3DFar`)**：天穹、云层、远景建筑；
     2. **地台相机 (`BgStage3DCamera`, depth = 主相机 - 1, 只清深度, `Layer 8: Map3D`)**：可走地台与相连近景；
     3. **正交主相机 (depth 0, 只清深度, 剔除 Layer 8 / 9)**：角色、阴影、特效、跳字。
     - 两台相机都按「地面纵深中线」(`Y = 0, Z = 0` 的直线) 求视野与投影矩阵移轴 (`m02 / m12`)，对齐主相机的画面中心与屏幕内旋转：地台相机的这条中线与角色脚底逐像素重合，**地台物距、机位高度、俯仰任意调都不滑步** (平移 / 缩放 / 旋转均已验证)。**不开放地台缩放与平移**，二者会破坏这一对齐。地台俯仰会让竖直物体上下汇聚 (三点透视)；机位高度会改变后沿护栏在画面上的位置。远景相机不承载角色，另开放视差倍率、上下偏移、视野倍率。
3. **3D 地图空间、图层与材质铁律**：
   - **空间尺寸**：可走地台顶面严格位于水平面 `Y = 0`；横向可走区 `X ∈ [-8m, +8m]`（模型外观左右须延伸至 `±16m` 防震屏/旋转露底）；地台不俯仰时逻辑纵深 `y` 对应地面 `Z = D·y / (s·D − y)`（`D` 为地台物距、`s` 为机位高度斜率；默认 32m / 0.368 时 `MinY / MaxY` ≈ `-2.5m / +3.0m`），后沿护栏底边放在该处。
   - **场地净空**：可走地台上方及相机正前方 (`Z < -3m`) **严禁摆放高大遮挡物**，所有建筑与装饰物必须位于后沿护栏之外 (`Z > +3.0m`) 或左右版边之外 (`|X| > 8.2m`)。
   - **图层划分**：地台、后沿护栏、与地台接壤的左右侧楼必须设为 `Layer 8 (Map3D)`；与地台断开的下层广场、远楼、天穹与云层设为 `Layer 9 (Map3DFar)`。其他图层的渲染器会被主相机画进角色层，`MapManager` 加载时会给出警告。
   - **材质与队列**：3D 场景统一用 `KiHan/Map/Lightmapped`（漫反射 × 色调 × UV2 光照贴图，不受实时灯光；开关 `_UseLightmap`，`_ZWrite / _SrcBlend / _DstBlend / _Cull / _Cutoff` 按类型设置，`_Exposure` 调明暗）。光照用烘焙贴图：外部资源自带的光照贴图直接接入，没有的自绘光照贴图 (UV2 铺满整面)。队列：天穹 `ZWrite Off` + `1000`；不透明 `2000`；镂空 (`_Cutoff = 0.33`) `2450`；半透明 (`ZWrite Off`, `SrcAlpha / OneMinusSrcAlpha`) `2900`。
   - **外部模型坐标系转换**：从 UE / glTF (右手系) 导入 Unity (左手系) 时，顶点坐标须转换为 `(X, Y, -Z)`、翻转三角形绕序 `(i0, i2, i1)`，且贴图 UV 纵向翻转 `v_unity = 1 - v_gltf`。
4. **四步标准接入流程**：
   - ① 网格 / 材质 / 贴图放入 `Assets/AssetPackages/Maps/<id>/` 的 `Meshes/`、`Materials/`、`Textures/`（不放 Res，由预制体引用打包）；
   - ② 场景预制体存为 `Assets/AssetPackages/Res/Maps/<id>/scen.prefab`；
   - ③ 创建 `Assets/AssetPackages/Maps/<id>/<id>.asset` (`MapConfig`)，填写 `id`、`displayName`、`scene`、`shot` (`320×443` 缩略图)，3D 地图勾选 `is3D` 并在 `camera3D` 中调该场景的相机参数 (`stageDistance`、`stageHeightSlope`、`stagePitch`、`farDistance`、`farHeightSlope`、`farPitch`、`farParallaxScale`、`farVerticalOffset`、`farZoomScale`、`farBackgroundColor`)；Play 模式下改该资源或 Main Camera Inspector 底部的「当前地图 3D 相机参数」即时生效并保留；
   - ④ 将 `<id>.asset` 登记到 `Assets/AssetPackages/Res/Config/MapRegistry.asset`。

---

## 十、 资源与 Lua 热更

1. **禁止 `Resources.Load`**：运行时资源一律走 `AssetManager.Instance.Load<T>` / `TryLoad<T>` (静默回退查找) / `LoadAll<T>` (单个文件的子资产)。
2. **地址规则**：相对 `Assets/AssetPackages/Res/` 的路径、不含扩展名 (如 `Config/MapRegistry`)；Lua 为 `Lua/` + 相对 `Assets/Lua/` 的路径 (`KiHan.Asset.AssetAddress`)。
3. **Lua 面板**：面板逻辑放 `Assets/Lua/ui/`，入口 `Assets/Lua/main.lua`；Lua 新用到的 `CS.` 类型必须同步加进 `Assets/Editor/Lua/XLuaGenConfig.cs` 的 `LuaCallCSharp` (编辑器走反射，IL2CPP 真机需 `XLua/Generate Code` 生成代码)。
4. **AB 与首包**：菜单 `KiHan/资源/构建 AB 包/Android|编辑器验证 (Win64)` 输出 `Build/Bundles/<target>/`，`KiHan/资源/写入首包/...` 写入首包；编辑器默认直接读 AssetDatabase，勾选 `KiHan/资源/编辑器使用 AB 包` 走 AB。
5. **热更范围**：只热更 Lua 与资源 (启动时 `HotUpdater`)，不启用 xLua `[Hotfix]`；C# 改动必须整包更新。
