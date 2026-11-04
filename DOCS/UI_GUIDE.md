# 新建 UI 规范与流程 (UI Guide)

> 框架结构见 [UI_FRAMEWORK.md](UI_FRAMEWORK.md)，编码细则 (按钮回弹、布局、性能) 见 [.agents/ui.md](../.agents/ui.md)。本文是动手做一个界面的步骤与检查清单。

---

## 一、 先定三件事

| 事项 | 规则 |
| :--- | :--- |
| 类型与命名 | 全屏界面 `XxxPanel`，弹窗 `XxxPopup`，可复用部件 `XxxView`；类名 = 预制体名 = 脚本文件名 |
| 层级 (`UILayer`) | 全屏界面 `Normal`；弹窗 `Popup` (模态：有遮罩、挡住下面的点击、返回键关闭)；战斗界面与**战斗中常驻、不挡其他按钮的面板** (如训练场设置) `Hud`；飘字 `Toast`；切换 Loading `Loading` |
| 模块目录 | 预制体 `Assets/AssetPackages/Resources/UI/Panels/<模块>/<类名>.prefab`，脚本 `Assets/Scripts/View/UI/<模块>/<类名>.cs` (模块如 `Login`、`Lobby`、`Common`、`BattleHud`；子功能再开子目录，如训练场 `BattleHud/Training/`)；模块专用部件放 `<模块>/Widgets/`。完整目录见 [UI_FRAMEWORK.md](UI_FRAMEWORK.md) 第 1 节 |

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

## 三、 写脚本

### 1. 先写空壳并挂上

脚本必须能编译后才能挂到预制体上，所以先写一个不使用任何绑定字段的空壳：

```csharp
namespace KiHan.UI
{
    [UIPanel("UI/Panels/Shop/ShopPanel", UILayer.Normal)]
    public partial class ShopPanel : UIPanel
    {
    }
}
```

- 必须是 `partial` (绑定字段在生成的 `ShopPanel.Bind.cs` 里)；
- `UIPanel` 的路径 = `Resources/` 之后、不含 `.prefab` 的路径，必须与预制体实际位置一致；
- 编译后把脚本挂到预制体**根节点**。

### 2. 标记节点并生成绑定

1. 把代码要用到的节点改名为 `@名字`：

   | 写法 | 生成的字段 |
   | :--- | :--- |
   | `@BuyBtn` (节点上有 Button) | `UnityEngine.UI.Button _buyBtn` |
   | `@Price` (节点上有 TMP 文本) | `TMPro.TMP_Text _price` |
   | `@Icon:Image` | 指定类型 `Image` |
   | `@Effect:GameObject` | 指定为 `GameObject` (只做显示隐藏时用) |
   | `@Item_0`、`@Item_1`、`@Item_2` | 数组 `_item` (下标从 0 连续) |

   未指定类型时按顺序取节点上第一个匹配的组件：`UIView` 子类 → `Button` → `Toggle` → `Slider` → `TMP_InputField` → `ScrollRect` → `TMP_Text` → `RawImage` → `Image` → `RectTransform`。
   名字只能用字母、数字、下划线；同一个面板 (不含子部件内部) 内不能重名。
2. 菜单 **KiHan/UI/生成全部绑定**：生成 / 更新 `<类名>.Bind.cs`，编译完成后自动把节点赋给字段 (日志出现 `[UIBind] 绑定赋值完成`)。
3. 之后增删 / 改名 `@` 节点都要重新执行一次；代码里引用了已删除的字段会直接编译报错。

### 3. 写逻辑

```csharp
using UnityEngine;

namespace KiHan.UI
{
    [UIPanel("UI/Panels/Shop/ShopPanel", UILayer.Normal)]
    public partial class ShopPanel : UIPanel
    {
        // 只调一次：按钮监听写在这里 (按钮已自动挂上按压回弹)
        protected override void OnCreate()
        {
            _buyBtn.onClick.AddListener(OnBuy);
            _closeBtn.onClick.AddListener(Close);
        }

        // 每次打开：刷新显示、订阅事件
        protected override void OnOpen()
        {
            _price.text = "100";
        }

        // 每次关闭：取消订阅 (与 OnOpen 成对)
        protected override void OnClose()
        {
        }

        private void OnBuy()
        {
            UIManager.Instance.Confirm("确定购买吗？", () => UIManager.Instance.Toast("购买成功"));
        }
    }
}
```

带参数打开的界面继承 `UIPanel<TData>`：

```csharp
[UIPanel("UI/Panels/Shop/BuyPopup", UILayer.Popup)]
public partial class BuyPopup : UIPanel<BuyPopup.Args>
{
    public struct Args
    {
        public int ItemId;
        public System.Action OnBought;
    }

    private Args _args;

    protected override void OnOpen(Args args)
    {
        _args = args;
    }
}
```

规则：
- 不写 `Awake` / `Start`；初始化放 `OnCreate`，订阅在 `OnOpen` / `OnClose` 成对处理；
- 不要自己 `SetActive` 显示 / 隐藏面板，关闭用 `Close()`；
- 禁止 `transform.Find` / 按名字查找 / `Resources.Load`；
- 只在数值变化时改 UI，文本用缓存字符串，计时用 `Time.unscaledDeltaTime`；
- 不直接调用 `BattleManager`、网络清理等流程代码：流程切换调用 `GameFlow` (见第五节)。

### 4. 打开 / 关闭

```csharp
UIManager.Instance.Open<ShopPanel>();
UIManager.Instance.Open<BuyPopup, BuyPopup.Args>(new BuyPopup.Args { ItemId = 1 });
UIManager.Instance.Close<ShopPanel>();
UIManager.Instance.Back();                 // 关最上层弹窗；没有弹窗时关 Normal 栈顶 (安卓返回键同效)
UIManager.Instance.Toast("提示文字");
UIManager.Instance.Confirm("确定吗？", onConfirm, onCancel);
```

- Normal 层是栈：打开新界面时下面的界面被隐藏 (不关闭)，关闭栈顶后恢复；
- 面板默认关闭后保留实例，下次打开复用并重新 `OnOpen`；不需要保留的面板写 `[UIPanel(..., Cache = false)]`。

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
   - 面板预制体在 `UIPanel` 路径上，根节点挂了同名脚本，文件名与类名一致；
   - `.Bind.cs` 与预制体的 `@` 节点一致；
   - 每个 `@` 字段都已赋值。
2. 控制台 0 错误 0 警告。
3. Device Simulator 里依次切换 **P40 Pro、一台 16:9 手机、一台 iPad** 看布局：靠边元素是否贴边、是否有重叠或超出屏幕。

## 八、 常见问题

| 现象 | 原因 / 处理 |
| :--- | :--- |
| 编译报 "缺少 partial 修饰符" | 类没写 `partial`；没有任何 `@` 节点的类不生成绑定文件，可以不写 |
| 报 "找不到面板预制体" | `UIPanel` 路径与预制体位置不一致 (注意不含 `Resources/` 与 `.prefab`) |
| 报 "根节点缺少 XxxPanel" | 脚本挂在了子节点上，或预制体根节点不是面板 |
| 字段是 null | 改了 `@` 节点后没重新执行 **生成全部绑定** |
| 生成的类型不对 | 节点上有更靠前的组件 (如同时有 Button 和 Image)，用 `@Name:Image` 指定 |
| "数组下标不连续" | `@Item_0`、`@Item_2` 中间缺 `_1` |
| 弹窗下面没有遮罩 / 点穿 | 层级写成了 `Normal`；弹窗用 `UILayer.Popup` |
| 界面在别的手机上错位 | 锚点没贴边；在 Simulator 换设备检查 |
