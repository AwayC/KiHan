# KiHan 测试规范 (Testing Rules)
# 更新时间：2026-10-11

本文档约定逻辑层与 Unity 侧的测试分层、写法与提交前检查。与 [rules.md](rules.md) 同为强制规范。

---

## 一、 测试分层

| 层级 | 位置 / 方式 | 依赖 | 覆盖范围 | 何时必须跑 |
|---|---|---|---|---|
| **L1 逻辑层命令行测试** | `Tests/LogicTests/`，`run_all.sh` | .NET 8 SDK，**不需要 Unity** | 战斗逻辑：动作、输入、物理、命中、受击流程、配置 DTO 转换、确定性、0 GC | 每次改动逻辑层代码或配置格式，提交前 |
| **L2 Unity 编辑器验证** | Unity 2022.3 打开工程，按第四节清单执行 | Unity | 真实引擎行为：双端编译、`.meta`、`JsonUtility` 加载、编辑器工具、资源序列化 | 改动 DTO 格式 / ScriptableObject / 编辑器工具 / 资源后；在无 Unity 环境完成的改动首次进入 Unity 时 |
| **L3 实机联机验证** | 场景运行 + 帧同步 | Unity + 网络 | 表现层、联机一致性 | `BattleWorld` 接入 `BattleManager` 之后 (当前不做) |

---

## 二、 L1 逻辑层命令行测试

### 1. 运行

```bash
Tests/LogicTests/run_all.sh
```

- 先以 `netstandard2.1` (与 Unity 2022.3 API 级别一致) 编译全部逻辑代码，再依次运行各测试工程。
- 任一编译失败、`[FAIL]`、未捕获异常或非零退出码，脚本返回非零并打印 `SOME TESTS FAILED`；全部通过打印 `ALL PASSED`。

### 2. 目录结构

```
Tests/LogicTests/
  LogicSources.props   # 共用：Unity 替身 + Assets/Scripts 下的真实逻辑源码 (相对路径)
  Stubs/               # Unity 替身 (UnityStubs.cs) 与 ButtonMask 副本
  Compile/             # 只做编译检查 (netstandard2.1)
  Combat/              # 主测试：带断言的场景用例
  Jump/                # 跳跃/滞空逐帧轨迹 + 确定性哈希
  HitGC/               # 连续命中 1000 帧的托管分配
  Hurt/ Stride/        # 历史逐帧打印用例 (人工核对，无断言)
  run_all.sh
```

- 测试编译的是仓库中的**真实源码**，不复制逻辑代码。
- `Tests/` 位于 `Assets` 之外，Unity 不会导入；`.gitignore` 已放行其中的 `*.csproj`，并忽略 `bin/`、`obj/`。

### 3. 用例写法

1. **按真实主循环驱动**：通过 `BattleWorld.Tick(RoomFrame)` 推进，输入经 `RoomFrame.InputFrames[槽位]` 注入 (摇杆角度字节 + `ButtonMask`)。只有在**搭建场景**时才直接调用内部方法 (如设置位置、`EnterLaunch`、`EnterLying`)，断言阶段不绕开主循环。
2. **场景相互独立**：每个场景新建角色与 `BattleWorld`。`AddCharacter` 会调用 `ResetCombatState`，需要预设的受击状态必须在加入之后设置。
3. **数据在代码里构造**：动画用 `AnimationFrameData` / `SpriteFrameData` 按需构造 (帧数、受击盒、攻击盒、锚点显式给出)，不读取 `.asset`。需要读取仓库文件 (如 `action_template.json`) 时，用基于源文件位置的 `RepoRoot()` 拼路径，禁止写死绝对路径。
4. **配置优先走 DTO**：新增或修改配置字段时，至少有一个用例通过 `ActionData` 等 DTO 的 `ToRuntime()` 构造动作，验证 String-First 解析链路。
5. **断言**：使用 `Check(条件, 描述)`，描述中必须带上**实际值**，期望值由公式推导写在描述里 (如 `预期 2.5-3×10/30=1.5`)。输出 `[OK]` / `[FAIL]`，`Main` 有失败时返回非零。**新用例不允许只打印不断言**；`Hurt/`、`Stride/` 为历史打印用例，保留作人工核对。
6. **时间口径**：一律以逻辑帧计数，不使用墙钟；与帧率相关的期望值由 `GameConfig.LOGIC_FPS` 推导。

### 4. 必测项 (按改动类型)

| 改动内容 | 必须覆盖 |
|---|---|
| 物理、积分、位移 (MotionBody / RootMotion / ForceMove) | `Jump` 确定性哈希两次运行一致；改动前后同一输入的哈希不变 (若语义有意改变，在提交说明中写明并更新基准值) |
| 每帧执行路径 (命中链路、抓取保持、RootMotion、移动、输入解析) | 预热后 `GC.GetAllocatedBytesForCurrentThread` 差值为 0 |
| 帧区间 / 判定窗口 | 区间首帧、末帧、动作切换当帧、跳帧 (一个逻辑帧推进多个动画帧时) |
| 同帧多个动作预约 | 优先级裁决：命中 (100) > 触地结果 (95) > 动画播完推进 (90) > 主动打断 / autoNext |
| 输入 / 指令 | 按下、松开、按住 (`Move`) 三种语义；按住跨越动作切换不连发；缓冲窗口边界 |
| 受击流程 | 状态 (`HurtState`) 与动作 id 同时断言；无敌期间不产生命中 |
| 配置格式 | 更新 `action_template.json` / `example.json`，`Combat` 中的模板加载用例 (场景 14) 必须通过 |
| Bug 修复 | 先写能复现问题的失败用例，再修复，修复后该用例保留 |

### 5. Unity 替身规则 (`Stubs/`)

1. 只放逻辑层实际用到的 Unity 类型，**不写任何游戏逻辑**。
2. 逻辑会依赖其**返回值**的 API (如 `Mathf.Max/Min/Clamp/RoundToInt`、`Vector2Int.RoundToInt`) 必须与 Unity 语义一致；只用于表现/日志的 API 可以是空实现。
   - 教训：曾有 `Mathf.Max` 替身直接返回第一个参数，掩盖了帧时长计算的问题。
3. 逻辑层新用到 Unity API 导致编译失败时，在替身中按真实语义补充。
4. `Stubs/ButtonMask.cs` 是 `Assets/Scripts/Managers/InputManager.cs` 中 `ButtonMask` 的副本，修改原枚举时必须同步。

### 6. 扩展

- 新增测试工程：`Tests/LogicTests/<Name>/<Name>.csproj` 导入 `../LogicSources.props`，并把 `<Name>` 加入 `run_all.sh` 的列表。
- 逻辑层新增源码目录：同步更新 `LogicSources.props` 的 `Compile Include`。逻辑层不得引用表现层 / Managers 代码，否则无法在 L1 编译 (这也是第五节建 asmdef 的前提)。

---

## 三、 提交前检查

1. `Tests/LogicTests/run_all.sh` 输出 `ALL PASSED`。
2. 改动涉及 Unity 侧 (资源、编辑器、DTO 格式、ScriptableObject) 时，在有 Unity 的环境完成第四节对应项后再合并；在无 Unity 环境完成的提交，提交说明中注明 **「未经 Unity 验证」**。
3. 新增 `.cs` 文件或目录后，在 Unity 中生成的 `.meta` 必须随之提交 (保证各机器 GUID 一致)。

---

## 四、 L2 Unity 验证清单

换到有 Unity 的环境后按顺序执行，结果记录到 TODO 或 issue。

### 1. 编译与元数据
- [ ] 打开工程等待导入完成，Console 中 `Assembly-CSharp` 与 `Assembly-CSharp-Editor` **0 错误、0 警告** ([rules.md](rules.md) 六.2)。
- [ ] 提交以下新文件自动生成的 `.meta` (当前缺失)：
  - `Assets/Scripts/Game/Physics/` (目录)、`Physics/FPBox.cs`、`Physics/MotionBody.cs`
  - `Assets/Scripts/Game/Core/BattleWorld.cs`
  - `Assets/Scripts/Game/Character/Action/RootMotionSegment.cs`
  - `Assets/Scripts/Game/Character/Fight/` 下的 `CombatTypes.cs`、`HitResolver.cs`、`HitResult.cs`、`HurtActionIds.cs`、`HurtState.cs`

### 2. 帧库与编辑器 (`SpriteLibraryEditor` / `naruto/frameLib.asset`)
- [ ] 打开帧库编辑器，判定盒、锚点均显示为整数像素；拖动绘制判定盒、Alt 拖动锚点后数值仍为整数。
- [ ] 判定盒列表的 `Tag` 字段、锚点列表 (`Name` + `Position`) 可正常编辑、新增、删除。
- [ ] 保存后 `frameLib.asset` 没有意外的大面积 diff (`Vector2Int` 序列化格式为 `{x: 11, y: 109}`)。

### 3. JSON 加载 (`JsonUtility`)
L1 中的模板加载用的是 `System.Text.Json`，与游戏实际使用的 `JsonUtility` 行为不完全一致，必须在 Unity 中复核：
- [ ] `JsonUtility.FromJson<ActionDataContainer>(action_template.json)` → 每个 `ToRuntime()`：动作数 9，`HurtActionIds.FindMissing` 为空，`SetAllActions` 无报错。
- [ ] `example.json` 去掉 `//` 注释后同样加载：重点核对字符串定点数 (`"0.6"`)、帧区间 `{ "x": .., "y": .. }` (`frameRange` / `frameRanges` / `range`)、`keySequence` 按键名 (`"Move"`、`"Skill1Release"`)、嵌套结构 (`rootMotion` 分段、`grab`、`suction`、`pushPower`)。
- [ ] 缺省字段的默认值：未配置的 `ActionChangeData` (class) 在 `JsonUtility` 下会被实例化为默认对象，`changeType = 0 (Keep)`，确认不会产生意外的动作切换。

### 4. 运行时抽查 (可选)
- [ ] 在编辑器中用测试场景或临时脚本创建两个角色，按 `action_template.json` 配置，确认 idle / move / 受击流程与 L1 结果一致。

---

## 五、 Unity 内自动化测试 (待建)

现状：已安装 Unity Test Framework 1.1.33，但游戏逻辑在默认程序集 `Assembly-CSharp` 中 (没有 asmdef)，带 asmdef 的测试程序集无法引用它。

- **方案 A (长期推荐)**：为逻辑层建立 asmdef (如 `KiHan.Logic`，覆盖 `LogicSources.props` 中的目录)。前置条件是逻辑层不依赖表现层与 Managers，需要先把 `ButtonMask` 从 `InputManager.cs` 拆到逻辑层。之后建立 EditMode 测试程序集引用 `KiHan.Logic` + NUnit，L1 用例可逐步迁移为 `[Test]`。
- **方案 B (过渡)**：测试放在 `Assets/Editor/Tests/` (编入 `Assembly-CSharp-Editor`，可直接引用游戏代码)。首次需确认该程序集能否引用 NUnit；不能则改用编辑器菜单运行器 (如 `KiHan/Run Logic Tests`)，复用 L1 用例，结果输出到 Console。
- 迁移时 JSON 加载改用 `JsonUtility`；`GC.GetAllocatedBytesForCurrentThread` 在 Unity Mono 下的可用性需先确认，不可用时改用 Profiler 的 GC Alloc 统计。
