# KiHan 架构重构与开发规范约定 (Project Specification)
# 更新时间：2026-10-10

本文档记录当前项目在 ACT 格斗框架集成与帧同步（Lockstep）改造过程中的核心强制规范。所有后续开发与子模块实现均须严格遵守。

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

---

## 三、 定点数体系与字符串规范 (Fixed-Point & String-First)

1. **逻辑内核变量全量定点数化**：
   - **位移与速度**：`FPVector3`、`FPVector2`、`FP moveSpeedX` / `FP moveSpeedY` (横向与纵深各自独立，摇杆只有 8 方向，斜向不归一化)、`MoveInputAcceptance.rate`（移动速度接受率）。
   - **重力与动力学**：`FP weight`、`_curWeight`、`MoveInfo.moveDistance`、`MoveInfo.inSec`。
   - **战斗与命中时钟**：`AttackInfo.attack`（伤害）、`AttackInfo.hitStun`（硬直）、`AttackInfo.freeze`（卡帧）、`AttackInfo.hitSameTargetDelay`、`HitRecord.Cooldown`。
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
     "keySequence": ["Down", "Skill1"],
     "validInSec": "0.05",
     "rate": "1.0",
     "hitStun": "0.12",
     "freeze": "0.05",
     "hitSameTargetDelay": "1.0",
     "pushPower": {
       "moveX": "0.2",
       "moveY": "0.0",
       "moveZ": "0.0",
       "inSec": "0.08",
       "tweenMethod": "Slowly"
     }
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
