# KiHan UI 框架 (UI Framework)

> 适用于全部 UGUI 界面。新建界面的步骤与检查清单见 [UI_GUIDE.md](UI_GUIDE.md)，编码细则 (按钮回弹、布局、性能) 见 [.agents/ui.md](../.agents/ui.md)。

## 1. 总览

```
GameFlow (流程：登录 → 大厅 → 战斗 → 大厅 / 登出)
   │ Open / Close / CloseLayer
   ▼
UIManager ── AssetManager (按地址加载面板预制体)
   │
   ▼
UIRoot (代码构建，常驻)
   ├─ Layer_Scene    sortingOrder 0
   ├─ Layer_Hud      100   战斗 HUD
   ├─ Layer_Normal   200   全屏界面 (返回栈)
   ├─ Layer_Popup    300   弹窗 (模态遮罩)
   ├─ Layer_Toast    400   飘字提示
   ├─ Layer_Loading  500   切换 Loading
   └─ Layer_System   600
```

- 每层一个 Canvas (Screen Space Overlay)，`CanvasScaler` 按屏幕尺寸缩放：参考分辨率 **2640×1200** (Device Simulator 华为 P40 Pro 横屏，现有界面均按它摆放)，按高度适配 (`match = 1`)。P40 Pro 上缩放 1，其他设备按屏幕高度等比缩放。
- 面板预制体**不带 Canvas**，根节点是铺满父节点的 `RectTransform`，打开时挂到对应层下。需要单独重建 (频繁变化) 的局部可以加子 Canvas。
- 每个面板 / 弹窗是独立预制体，用到时才加载：`Resources/UI/Panels/<模块>/<类名>.prefab`，代码在 `Scripts/View/UI/<模块>/`。

### 目录

```
Assets/Scripts/View/UI/
├─ Framework/            UIManager、UIPanel、UIView、UILayer、UITransition、SafeArea、UIPressBounce
├─ Common/               通用面板：ConfirmPopup、ToastPanel、LoadingPanel
│  └─ Widgets/           通用部件：UIScrollList (滑动列表 / 网格)、UISwitch (开关，关压暗 / 开原色)、UIHoldButton (按住 / 松开)、UIDigitRow、DigitFont、UIRotator、UIBreathe (呼吸灯)、NinjaCardView (忍者卡片)
├─ Login/                LoginPanel、LoginPopup、RegisterPopup、PasswordVisibility
├─ Lobby/                LobbyPanel、CreateRolePopup、MatchingPopup
└─ BattleHud/            BattleHudPanel
   ├─ Widgets/           HUD 部件：FighterStatusView、HpBarView、BeanListView、LightIcon、技能 / 替身 / 通灵按键、SkillWindowView (接段窗口圆形倒计时)、ButtonTimerView (按键上的 Buff 剩余时间圆环，显示 buffs.json 中配置了 button 的 Buff)、SkillIconCache
   ├─ Input/             VirtualButton、VirtualJoystick (直接写入 InputManager)
   └─ Training/          训练场：MapSelectPopup (选图)、MapItemView、CharSelectPopup (换人)、CharSelectItemView、TrainingSettingView (设置与伤害统计，嵌在 HUD 中)

Assets/AssetPackages/Resources/UI/
├─ Panels/<模块>/        面板 / 弹窗预制体，目录与脚本模块一一对应 (训练场：Panels/BattleHud/Training/)
├─ Widgets/              被面板嵌套引用的通用预制体：Slider、Loading/seque、Loading/round、Common/NinjaCard (忍者卡片)、BattleHud/CDTimer (技能按键的接段倒计时圆环)
├─ Number/               伤害数字字体 (DamageTextPool 按路径加载)
└─ Shadow/               角色脚下阴影 (CharacterFootShadow 按路径加载)
```

- 模块下的子功能 (如训练场) 再开子目录，面板路径同步：`[UIPanel("UI/Panels/BattleHud/Training/XxxPopup", UILayer.Popup)]`。
- 只被某个模块用到的部件放该模块的 `Widgets/`；被两个以上模块使用时移到 `Common/Widgets/`。
- 移动脚本 / 预制体时连同 `.meta` 一起移动 (或在 Unity 里拖动)，保持 GUID 不变，预制体上的引用不会丢；`.Bind.cs` 跟随类脚本所在目录生成。

## 2. 面板 (`UIPanel`)

```csharp
[UIPanel("UI/Panels/Lobby/LobbyPanel", UILayer.Normal)]
public partial class LobbyPanel : UIPanel { ... }

[UIPanel("UI/Panels/Common/ConfirmPopup", UILayer.Popup)]
public partial class ConfirmPopup : UIPanel<ConfirmPopup.Args> { ... }
```

| 生命周期 | 时机 |
| :--- | :--- |
| `OnCreate()` | 实例化后只调一次 (绑定已就绪；给按钮加监听) |
| `OnOpen()` / `OnOpen(TData)` | 每次打开 (显示前) |
| `OnClose()` | 每次关闭 (隐藏动画前；取消订阅) |
| `OnRelease()` | 销毁前 (`Cache = false` 或场景清理) |

- 面板自己不调用 `SetActive` 显示 / 隐藏自己，统一走 `Close()` / `UIManager`。
- 按钮在 `OnCreate` 前自动挂上 `UIPressBounce` (见 ui.md)。
- `UIPanelAttribute`：`Path` 资源地址，`Layer` 层级，`Cache` (默认 true，关闭后保留实例)。

## 3. 管理器 (`UIManager`)

| API | 说明 |
| :--- | :--- |
| `Open<T>()` / `Open<T, TData>(data)` | 打开 (已打开则提到最前并重新 `OnOpen`) |
| `Close<T>()` / `Close(panel)` | 关闭 |
| `Back()` | 关闭最上层弹窗；没有弹窗时关闭 Normal 栈顶并恢复下一个 |
| `CloseLayer(layer)` | 关闭某一层全部面板 (流程切换时) |
| `Get<T>()` / `IsOpen<T>()` | 查询 |
| `Toast(msg)` | 飘字提示 (排队显示) |
| `Confirm(text, onConfirm, onCancel)` | 通用确认弹窗 |

- **Normal 层是栈**：打开新界面时隐藏 (不关闭) 下面的界面，关闭栈顶时恢复下一个。
- **Popup 层模态**：最上层弹窗下方自动放一张半透明遮罩挡住点击。
- **Hud 层非模态**：没有遮罩、不进返回栈、返回键不关闭，后打开的面板显示在上面；只有面板里真正有图形 (`raycastTarget`) 的区域挡点击，其余位置照常点到下面的 HUD 按钮。战斗中常驻的面板 (如训练场设置) 放这一层，由自己的按钮关闭；`GameFlow.ExitBattle` 关闭整层。根节点不要挂铺满屏幕的 `Image`，否则整屏都会挡住。
- 显示 / 隐藏动画由面板根节点上的 `UITransition` 组件决定 (缩放 / 淡入 / 滑入，`unscaledDeltaTime`，无协程)；没有该组件时立即显示 / 隐藏。

## 4. 部件 (`UIView`)

- 可复用部件继承 `UIView`，对外只提供 `Bind` / `Refresh` 等少量入口 (如 `FighterStatusView`、`SkillButtonView`)。
- `UIView` 是绑定的边界：面板生成绑定时不深入部件内部，部件自己的节点由部件自己绑定。

### 滑动列表 (`UIScrollList`)

通用横向 / 纵向列表，可选网格：拖动跟手，松手惯性减速，越界橡皮筋阻尼并弹性回弹；可选停靠 (松手停在最近一行并居中，轻甩翻一行)；只创建可见项并循环复用。

```
@MapList            ← UIScrollList + RectMask2D (裁剪) + 透明 Image (空白处也能拖)
└─ Content          ← 铺满视口 (_content)
   └─ Template      ← 项模板，隐藏 (_template)，根节点挂项部件 (如 MapItemView)，大小即项大小
```

```csharp
_mapList.SetCount<MapItemView>(_maps.Count, (view, index) => view.Bind(_maps[index]));   // 项进入可见范围时回调
_mapList.ScrollTo(current, animated: false);
_mapList.RefreshItems();                                                                // 数据变化、数量不变
```

| 参数 | 说明 |
| :--- | :--- |
| `Direction` | 横向 / 纵向 |
| `Spacing` / `Padding` | 行间距 / 首尾留白 |
| `Per Line` / `Cross Spacing` | 网格：每行 (纵向) / 每列 (横向) 的项数与行内间距；项在行内居中排列，末行从行首排起 (如换人弹窗 6 列) |
| `Snap` | 停靠模式；`CenterChanged` 事件 / `CenterIndex` 给出居中行的第一项 |
| `Deceleration Rate` | 惯性每秒保留的速度比例 (默认 0.135，同原生 ScrollRect) |
| `Spring Time` | 回弹 / 停靠 / 动画定位的平滑时间 |
| `Side Scale` | 离开中心一项距离时的缩放 (停靠模式下做轮播效果) |

内容不足一屏时整体居中，仍可拖动并回弹。绑定回调建议缓存为字段 (`_bindItem = BindItem`)，避免每次打开分配委托。

列表项会被复用：选中等状态由面板按下标记录，绑定时传给项 (如 `CharSelectPopup` 的 `_selectedIndex`)，选中变化后 `RefreshItems()`；不要把状态存在项上。

## 5. 自动绑定 (`@` 前缀)

1. 在预制体里把需要被代码引用的节点改名为 `@名字` (如 `@StartBtn`)。
2. 菜单 **KiHan/UI/生成绑定 (选中预制体)** 或 **KiHan/UI/生成全部绑定**：
   - 生成 `<类名>.Bind.cs` (与面板脚本同目录的 `partial` 类)，每个 `@` 节点一个 `[SerializeField]` 字段；
   - 脚本编译后自动把节点拖进预制体对应字段。
3. 面板 / 部件类必须声明为 `partial`，代码里直接使用生成的字段。

| 规则 | 说明 |
| :--- | :--- |
| 字段名 | `@StartBtn` → `_startBtn` |
| 类型推断 | 按顺序取节点上第一个匹配的组件：`UIView` 子类 → `Button` → `Toggle` → `Slider` → `TMP_InputField` → `ScrollRect` → `TMP_Text` → `RawImage` → `Image` → `RectTransform` |
| 指定类型 | `@Name:GameObject` / `@Name:Image` 等 (冒号后写类型名) |
| 数组 | `@Skill_0`、`@Skill_1` … 同名同类型合并为数组 `_skill` |
| 边界 | 不深入带 `UIView` / `UIPanel` 组件的子节点 (节点本身可绑定) |

- 校验 (Unity 测试 / 菜单 **KiHan/UI/校验全部面板**)：预制体根节点挂了对应面板脚本、`@` 字段全部已赋值、同一作用域内无重名、`UIPanelAttribute.Path` 与预制体位置一致。
- 禁止 `transform.Find` / 按名字查找 (ui.md 二.1)。

## 6. 流程 (`GameFlow`)

| 方法 | 作用 |
| :--- | :--- |
| `EnterLogin()` | 关闭 Normal / Popup 层，打开登录 |
| `EnterLobby()` | 关闭 Normal / Popup 层，打开大厅 |
| `StartOfflineBattle()` / `StartOnlineBattle(roomId)` | 进入战斗 (关闭大厅，打开 HUD) |
| `ExitBattle()` | Loading 中清理战斗，回到大厅 |
| `Logout()` | Loading 中断开网络并清理，回到登录 |
| `RestartTraining(slot, characterId)` | 训练场换人：Loading 中清理战斗，换掉该槽位的角色重开 (地图保持，训练面板开关回到默认) |
| `ChangeMap(map)` | 训练场切换地图：Loading 中替换场景背景板 (`MapManager.ChangeScene`)，完成后恢复战斗 |
| `RunTransition(routine, onFinish)` | 显示 `LoadingPanel` 执行切换 |

界面只调用 `GameFlow` 的意图方法 (开始匹配、退出战斗、登出)，不直接调用 `BattleManager` / 网络清理。

## 7. 面板清单

| 模块 | 面板 | 层 | 来源 (旧预制体节点) |
| :--- | :--- | :--- | :--- |
| Common | `ToastPanel` | Toast | `SystemTipPanel/TipPanel` |
| Common | `ConfirmPopup` | Popup | 大厅 `BackPanel` (战斗 HUD 返回确认复用) |
| Common | `LoadingPanel` | Loading | `Loading/Canvas` |
| Login | `LoginPanel` | Normal | `LoginPanel/Canvas` (去掉两个弹窗) |
| Login | `LoginPopup` / `RegisterPopup` | Popup | `LoginPanel/Canvas` 的 `LoginPanel` / `RegisterPanel` 节点 |
| Lobby | `LobbyPanel` | Normal | `Lobby/Canvas` (去掉两个弹窗) |
| Lobby | `CreateRolePopup` | Popup | `Lobby/Canvas` 的 `CreatePlayerPanel` 节点 |
| Lobby | `MatchingPopup` | Popup | `Lobby/MatchPanel` |
| BattleHud | `BattleHudPanel` | Hud | `Button/Canvas` (返回确认改用 `ConfirmPopup`) |
| BattleHud/Training | `MapSelectPopup` | Popup | 新增：HUD 场景按钮打开，列出 `MapRegistry` 中已启用的地图 (含 2D 与 3D 地图)，选择后经 `GameFlow.ChangeMap` 播放 Loading 动画切换场景 |
| BattleHud/Training | `CharSelectPopup` | Popup | 新增：角色状态上的换人按钮打开 (仅训练场)，上下滑动的 6 列网格列出 `CharacterRegistry` 中已启用的忍者，选中框呼吸灯，确定后重开训练场 |

战斗伤害跳字为世界空间 `SpriteRenderer`，不属于 UGUI，不在本框架内。

## 8. 安全区

`SafeArea` 组件把节点锚定到 `Screen.safeArea`。面板根节点默认铺满屏幕 (保持现有布局)；需要避开挖孔 / 刘海的按钮放进面板内带 `SafeArea` 的子节点，背景与遮罩留在外面铺满。
