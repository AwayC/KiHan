# KiHan UI 开发规范 (UI Specification)
# 更新时间：2026-10-24

> 适用于 UGUI 界面 (面板、战斗 HUD、弹窗)。新写或修改 UI 时遵守；与现有代码冲突时以本文为准并顺手迁移。

---

## 一、 按钮交互

1. **按压动画统一用 Q 弹缩放 (`UIPressBounce`)**：
   - 所有没有特殊设计的按钮都必须带按压回弹：按下缩小到 0.88，松开弹回并小幅过冲后回稳。
   - 面板 (`BasePanel` 子类) 在 `Awake` 时自动给面板内所有 `Button` 挂上，无需手动添加；战斗虚拟按键 (`VirtualButton`) 自动挂上。
   - 运行时动态生成的 UI (不经过 `BasePanel` 的预制体实例) 生成后调用 `UIPressBounce.AttachToButtons(go)`。
   - 非 `Button` 的可点击节点需要回弹时，直接在预制体上添加 `UIPressBounce`。
   - 手感参数 (缩放 / 刚度 / 阻尼) 保持默认，确有需要时在该按钮的 `UIPressBounce` 上单独调整，不改默认值。
2. **有特殊按压设计的按钮**：挂 `UIPressBounceIgnore`，自动挂载会跳过它，再自行实现表现。禁止为了绕开自动挂载去删除或禁用 `UIPressBounce`。
3. **面板 `Awake` 必须调用基类**：`BasePanel` 子类写成 `protected override void Awake()`，第一行 `base.Awake()`；写成 `private void Awake()` 会隐藏基类方法，按钮不会自动带动画。
4. **不要禁用交互组件来省 Update**：EventSystem 不向禁用的组件派发点击事件；需要空闲时不计算，用标志位在 `Update` 开头直接返回。
5. **按钮不可用**：用 `Button.interactable = false`，此时不播放按压动画；不要只换图不改 `interactable`。

---

## 二、 节点与绑定

1. **节点引用全部序列化**：脚本用 `[SerializeField]` 字段，在预制体中拖拽绑定；禁止运行时按名字 `transform.Find` / 递归查找节点，禁止为找不到节点写备用名回退。
2. **一排同类子节点 (豆、列表项)** 可在 `Awake` 中 `GetComponentsInChildren` 收集一次 (增减子节点即可改个数)，之后只用缓存。
3. **UI 节点必须是 `RectTransform`**：在 Canvas 下用"右键 → UI"创建，或复制已有 UI 节点；Canvas 外 Create Empty 再拖进来会得到普通 Transform，锚点、Layout、SafeArea 都会失效。
4. **命名**：
   - 面板预制体 / 类：名词 + `Panel` (如 `BattleHud` 挂 `BattleHudPanel`)。
   - 可复用部件 / 类：名词 + `View`，节点名为类名去掉 `View` (如 `HpBar` 挂 `HpBarView`)。
   - 多份实例用 `_P1` / `_P2` (按槽位，不按左右)，数组用 `_0`、`_1`。
   - 同一预制体内命名风格保持一致，不混用前缀与后缀写法。
5. **对外入口唯一**：组合部件 (如 `FighterStatusView`) 对外只提供 `Bind` / `Refresh`，外部不直接操作其子节点。

---

## 三、 布局与适配

1. **不用 Scale 调大小**：节点 Scale 保持 1，用 Width / Height 摆尺寸；Scale 会让九宫格两端变形、Layout 按未缩放尺寸计算间距。
2. **九宫格 (Sliced)**：在 Sprite Editor 设置 Border，Image Type 选 Sliced，改 Width 拉伸；两端显示粗细用 `Pixels Per Unit Multiplier` 调。需要按比例显示 (血条) 时用 `RectMask2D` 裁切 (`HpBarView`)，不用 Filled (会裁掉端头)。
3. **`RectMask2D` 不能被镜像**：遮罩节点在世界空间绕 Y 轴 180° 时裁切矩形宽度为负、内容全被裁掉；需要镜像的内容让遮罩反向旋转抵消，内容自己镜像。
4. **Layout Group**：
   - 只排列 `RectTransform` 子节点；内容比容器宽时 Child Alignment 失效 (从左边开始排)。
   - 需要按内容伸缩时加 `Content Size Fitter (Preferred Size)`，不勾 Child Force Expand，间距用正数。
   - 运行时隐藏某一项时只关图片保留占位 (如 `BeanListView` 的 Hidden 状态)，不要 `SetActive(false)` 触发重新排版，除非设计就是要重排。
5. **安全区**：需要避开刘海的 UI 放在 `SafeArea` 节点下 (锚点贴安全区)，背景、全屏遮罩放在外面铺满屏幕。

---

## 四、 刷新与性能

1. **只在数值变化时修改 UI**：视图缓存上次显示的值，不变直接返回；文本 / 数字用预生成字符串或图片数字 (`UIDigitRow` / `DigitFont`)，局内 0 GC。
2. **表现层可以用 float 与 `Time.unscaledDeltaTime`**：HUD 动画不受暂停与慢动作影响；不得回写逻辑数据 (逻辑层规则见 [rules.md](rules.md))。
3. **透明度动画优先 `CanvasRenderer.SetAlpha`**，不改 `Graphic.color` (后者触发网格重建)。
4. **纯显示的图片取消 Raycast Target**。
5. **同屏 UI 图片尽量放进同一图集**；自定义材质 (去色、叠加) 会打断合批，只用于确实需要的节点。

---

## 五、 特殊效果

1. **黑底光效图 (光圈、光晕)** 用叠加材质 `GUI/Materials/UIAdditive.mat`，黑色即透明；不要直接用默认材质。
2. **技能图标不可用 / CD 中** 用黑白材质 `UIGrayCooldown.mat` / `UIGrayLocked.mat`，不要另做灰色贴图。
3. **图片数字**：新建 `DigitFont` 资源，按数值 0~9 引用切片 (与切图顺序、切片名称无关)，`scale` / `spacing` 在资源上调。
