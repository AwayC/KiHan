# 新建 UI 规范与流程 (UI Guide)

> 框架结构见 [UI_FRAMEWORK.md](UI_FRAMEWORK.md)，编码细则 (按钮回弹、布局、性能) 见 [.agents/ui.md](../.agents/ui.md)。本文是动手做一个界面的步骤与检查清单。

---

## 一、 先定三件事

| 事项 | 规则 |
| :--- | :--- |
| 类型与命名 | 全屏界面 `XxxPanel`，弹窗 `XxxPopup`，可复用部件 `XxxView`；面板名 = 预制体名 = Lua 文件名，部件类名 = 预制体名 = 脚本文件名 |
| 层级 (`UILayer`) | 全屏界面 `Normal`；弹窗 `Popup` (模态：有遮罩、挡住下面的点击、返回键关闭)；战斗界面与**战斗中常驻、不挡其他按钮的面板** (如训练场设置) `Hud`；飘字 `Toast`；切换 Loading `Loading` |
| 模块目录 | 预制体 `Assets/AssetPackages/Res/UI/Panels/<模块>/<名字>.prefab`，Lua `Assets/Lua/ui/<模块小写>/<名字>.lua` (模块如 `Login`、`Lobby`、`Common`、`BattleHud`；子功能再开子目录，如训练场 `BattleHud/Training/` ↔ `ui/battlehud/training/`)；C# 部件放 `Assets/Scripts/View/UI/<模块>/Widgets/`。完整目录见 [UI_FRAMEWORK.md](UI_FRAMEWORK.md) 第 1 节 |

## 二、 做预制体

1. **布局环境**：Device Simulator 选 **Huawei P40 Pro 横屏 (2640×1200)**，所有界面按它摆放。运行时画布参考分辨率就是 2640×1200、按高度适配，P40 Pro 上所见即所得，其他设备按屏幕高度等比缩放。
2. **新建**：
   - 推荐复制一个现有预制体当模板 (全屏界面复制 `Lobby/LobbyPanel`，弹窗复制 `Common/ConfirmPopup`)，改名后删掉不要的节点；
   - 或在场景里任意 Canvas 下 "右键 → UI" 建节点，拖进目标目录成为预制体后删除场景里的实例。
3. **根节点**：
   - `RectTransform` 铺满父节点 (锚点 0~1，四边偏移 0)，Scale 1；
   - **不要**挂 `Canvas` / `CanvasScaler` / `GraphicRaycaster` (由层级画布提供)；
   - 弹窗**不要**自己做全屏暗色遮罩 (Popup 层自动提供)。
4. **子节点**：全部用 UI 节点 (`RectTransform`)；靠边的元素锚在对应的边 / 角上，避免在 16:9 或 iPad 上挤到一起；需要避开挖孔的按钮放进挂了 `SafeArea` 的子节点 (铺满父节点)，背景放在外面。
5. 预制体模式下编辑：Unity 会自动给没有 Canvas 的 UI 预制体套一个预览画布。

## 三、 写面板 (Lua)

### 1. 挂 `LuaPanel`

预制体**根节点**加组件 `KiHan.UI.LuaPanel`：
- `Script` 填 Lua 模块名，如 `ui.shop.ShopPanel` (对应 `Assets/Lua/ui/shop/ShopPanel.lua`)；
- `Layer` 选层级；`Cache` 默认勾选 (关闭后保留实例，下次打开复用并重新 `OnOpen`)，不需要保留的面板取消勾选。

### 2. 标记节点并生成绑定

1. 把代码要用到的节点改名为 `@名字`：

   | 写法 | Lua 中的字段 |
   | :--- | :--- |
   | `@BuyBtn` (节点上有 Button) | `self.buyBtn` (`Button`) |
   | `@Price` (节点上有 TMP 文本) | `self.price` (`TMP_Text`) |
   | `@Icon:Image` | 指定类型 `Image` |
   | `@Effect:GameObject` | 指定为 `GameObject` (只做显示隐藏时用) |
   | `@Item_0`、`@Item_1`、`@Item_2` | 数组 `self.item` (Lua 下标从 1 开始，`@Item_0` 是 `self.item[1]`) |

   未指定类型时按顺序取节点上第一个匹配的组件：`UIView` 子类 → `Button` → `Toggle` → `Slider` → `TMP_InputField` → `ScrollRect` → `TMP_Text` → `RawImage` → `Image` → `RectTransform`。
   名字只能用字母、数字、下划线；同一个面板 (不含子部件内部) 内不能重名。
2. 菜单 **KiHan/UI/生成全部绑定**：把节点写进 `LuaPanel` 的引用表 (`_bindings`)，面板不生成 `.Bind.cs`。
3. 之后增删 / 改名 `@` 节点都要重新执行一次 (校验会报引用表与节点不一致)。

### 3. 写逻辑

```lua
local Panel = require "ui.base.Panel"
local M = class("ShopPanel", Panel)

-- 只调一次：按钮监听、C# event 订阅写在这里 (按钮已自动挂上按压回弹)
function M:OnCreate()
    self:OnClick(self.buyBtn, self.OnBuy)
    self:OnClick(self.closeBtn, self.Close)
end

-- 每次打开：data 为 UI.Open 传入的 Lua 表或 C# 对象；Listen 订阅的委托字段在关闭时自动移除
function M:OnOpen(data)
    self.price.text = "100"
    self:Listen(CS.KiHan.Network.LobbyManager.Instance, "OnPlayerDataUpdated", self.Refresh)
end

function M:OnBuy()
    UI.Confirm("确定购买吗？", function() UI.Toast("购买成功") end)
end

return M
```

- 可重写 `OnCreate()` / `OnOpen(data)` / `OnClose()` / `OnRelease()`；需要每帧逻辑时定义 `Update(dt)` (`dt` 为 unscaled，打开期间每帧调用)；
- 带参数打开：`UI.Open("BuyPopup", { ItemId = 1, OnBought = fn })`，`OnOpen(data)` 里读 `data.ItemId` (字段 PascalCase)；
- C# `event` (如 `PageTabView.Clicked`) 在 `OnCreate` 里 `obj:Clicked("+", fn)`；
- 新用到的 `CS.` 类型加进 `Assets/Editor/Lua/XLuaGenConfig.cs` 的 `LuaCallCSharp`；C# 返回的整数可能是 Lua 浮点，拼显示字符串用 `core/mathx.lua` 的 `RoundToInt`。

规则：
- 初始化放 `OnCreate`，委托字段订阅用 `self:Listen`；
- 不要自己 `SetActive` 显示 / 隐藏面板，关闭用 `self:Close()`；
- 禁止 `transform.Find` / 按名字查找 / `Resources.Load`；
- 只在数值变化时改 UI，文本用缓存字符串 (模块级 Lua 表)；
- 不直接调用 `BattleManager`、网络清理等流程代码：流程切换调用 `GameFlow` (见第五节)。

### 4. 登记与打开 / 关闭

在 `Assets/Lua/ui/UIDef.lua` 加一项 `ShopPanel = "UI/Panels/Shop/ShopPanel"` (地址 = `Res/` 之后、不含 `.prefab` 的路径)；C# 也要打开时再加到 `KiHan.UI.UIPanels`。

```lua
UI.Open("ShopPanel")                       -- 返回 Lua 实例
UI.Open("BuyPopup", { ItemId = 1 })
UI.Close("ShopPanel")
UI.IsOpen("ShopPanel") / UI.Get("ShopPanel")
UI.Toast("提示文字")
UI.Confirm("确定吗？", onConfirm, onCancel)
```

```csharp
var loading = (LuaPanel)UIManager.Instance.Open(UIPanels.Loading);
loading.Call("SetProgress", 0.5f);         // 调 Lua 实例方法
UIManager.Instance.Close(UIPanels.Loading);
UIManager.Instance.Back();                 // 关最上层弹窗；没有弹窗时关 Normal 栈顶 (安卓返回键同效)
```

- Normal 层是栈：打开新界面时下面的界面被隐藏 (不关闭)，关闭栈顶后恢复。

## 四、 动画

- 在预制体根节点挂 `UITransition`，选类型：`Scale` (弹窗常用)、`Fade`、`SlideLeft` / `SlideRight` / `SlideTop` / `SlideBottom`，可调显示 / 隐藏时长；
- 不挂则立即显示 / 隐藏；不要在面板代码里写打开 / 关闭动画。

## 五、 流程切换 (`GameFlow`)

整屏切换走 `GameFlow`，它负责关掉旧界面、显示 Loading、处理战斗与网络：

| 场景 | 调用 |
| :--- | :--- |
| 进入登录 / 大厅 | `GameFlow.Instance.EnterLogin()` / `EnterLobby()` |
| 开始战斗 | `StartOfflineBattle()` / `StartOnlineBattle(roomId)` |
| 退出战斗 | `ExitBattle()` |
| 登出 | `Logout()` |
| 训练场切换地图 | `ChangeMap(map)` |
| 新的带 Loading 的切换 | `RunTransition(协程, 完成回调)` |

新增流程 (如选人、结算) 在 `GameFlow` 中加方法，界面只调用它。

## 六、 可复用部件 (`UIView`)

```csharp
namespace KiHan.UI
{
    public partial class PriceTagView : UIView
    {
        public void Bind(int price) { ... }
    }
}
```

- 继承 `UIView`，对外只给 `Bind` / `Refresh` 等少量方法，外部不操作其子节点；
- 部件内部的 `@` 节点由部件自己的 `.Bind.cs` 绑定；面板生成绑定时**不会深入**带 `UIView` 的节点，所以部件内部的名字不会和面板冲突；
- 面板引用部件：把部件根节点命名为 `@PriceTag`，生成的字段类型就是 `PriceTagView`；
- 部件做成独立预制体后嵌套使用时，部件内部的节点名在源预制体里改，不在外层改。

## 七、 检查与验收

1. 菜单 **KiHan/UI/校验全部面板** (Unity 自带测试也会执行)：
   - `LuaPanel` 的 `Script` 非空且 Lua 文件存在；
   - `_bindings` 与预制体的 `@` 节点一致；
   - 部件 `.Bind.cs` 与 `@` 节点一致、每个字段都已赋值。
2. 控制台 0 错误 0 警告。
3. Device Simulator 里依次切换 **P40 Pro、一台 16:9 手机、一台 iPad** 看布局：靠边元素是否贴边、是否有重叠或超出屏幕。

## 八、 常见问题

| 现象 | 原因 / 处理 |
| :--- | :--- |
| 编译报 "缺少 partial 修饰符" | 部件类没写 `partial`；没有任何 `@` 节点的类不生成绑定文件，可以不写 |
| 报 "找不到面板预制体" | `UIDef.lua` / `UIPanels` 地址与预制体位置不一致 (地址不含 `Assets/AssetPackages/Res/` 与 `.prefab`) |
| 报 "根节点缺少 LuaPanel" | `LuaPanel` 挂在了子节点上 |
| 字段是 nil / null | 改了 `@` 节点后没重新执行 **生成全部绑定** |
| 生成的类型不对 | 节点上有更靠前的组件 (如同时有 Button 和 Image)，用 `@Name:Image` 指定 |
| "数组下标不连续" | `@Item_0`、`@Item_2` 中间缺 `_1` |
| 弹窗下面没有遮罩 / 点穿 | 层级写成了 `Normal`；弹窗用 `UILayer.Popup` |
| 界面在别的手机上错位 | 锚点没贴边；在 Simulator 换设备检查 |
