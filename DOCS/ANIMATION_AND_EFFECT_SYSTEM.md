# KiHan 动画数据与特效渲染系统技术方案 (ANIMATION_AND_EFFECT_SYSTEM)

> **文档定位**：指导项目中 2D 序列帧动画数据、关键帧事件（Events）、编辑器工具（SpriteLibraryEditor / AnimationEditor）以及 3D 纵深特效渲染（EffectView / EffectManager）的开发规范。

---

## 目录
- [一、 特效渲染系统重构 (EffectView & EffectManager)](#一-特效渲染系统重构-effectview--effectmanager)
  - [1. 空间定位：3D 真实世界深度与挂点附着](#1-空间定位3d-真实世界深度与挂点附着)
  - [2. 全类型动画播放与自动回收机制](#2-全类型动画播放与自动回收机制)
  - [3. 彻底告别 2D 手算 sortingOrder](#3-彻底告别-2d-手算-sortingorder)
- [二、 动画数据结构改造 (AnimationFrameData & SpriteLibrary)](#二-动画数据结构改造-animationframedata--spritelibrary)
  - [1. 关键帧事件扩展 (FrameStepEvent)](#1-关键帧事件扩展-framestepevent)
  - [2. 判定盒合理尺寸与厚度预设](#2-判定盒合理尺寸与厚度预设)
- [三、 动画与切片帧编辑器 Bug 修复清单](#三-动画与切片帧编辑器-bug-修复清单)
  - [1. 帧库编辑器 (SpriteLibraryEditor) 修复](#1-帧库编辑器-spritelibraryeditor-修复)
  - [2. 动画时间轴编辑器 (AnimationEditor) 修复](#2-动画时间轴编辑器-animationeditor-修复)
- [四、 实操代码示例](#四-实操代码示例)

---

## 一、 特效渲染系统重构 (EffectView & EffectManager)

### 1. 空间定位：3D 真实世界深度与挂点附着
原 `EffectNode.cs` 将所有特效强行固定在 $Z=0$ 平面，导致与 3D 人物空间脱节。新版 `EffectView.cs`（位于 `Assets/Scripts/View/Render/EffectView.cs`）实现了两类定位：

1. **附着型特效 (Attached Effect)**：
   - 当 `EffectData.BindEntity != null` 时，自动查找角色的 `ActorView.Sockets` 挂点组件。
   - 自动绑定至目标挂点（如 `SocketType.Chest`, `HandR`），并随角色面向翻转（`FlipX`）做对称镜像。
2. **独立世界特效 (World Effect)**：
   - 当无绑定实体时（如地面击地烟尘、原地爆炸）：
     $$X = data.WorldPos.x + data.Offset.x \times 0.01f$$
     $$Y = (data.Height + data.Offset.y) \times 0.01f\text{ (垂直高度)}$$
     $$Z = data.WorldPos.y\text{ (决斗场纵深真实深度)}$$
   - 与角色处于完全相同的 3D 渲染坐标空间中，由 GPU Z-Buffer 自动判定遮挡。

### 2. 全类型动画播放与自动回收机制
- **全类型动画支持**：`EffectView` 在生成时自动扫描并唤醒内部挂载的：
  - **3D 粒子系统 (`ParticleSystem[]`)**
  - **2D 序列帧控制器 (`Animator`)**
  - **Timeline 镜头与序列 (`PlayableDirector`)**
- **全自动 0 GC 回收**：
  - 实现 `IPoolable` 契约，自动监听粒子存活状态与动画播放完毕。
  - 播放结束后全自动调用 `ObjectPoolManager.Instance.Recycle(gameObject)` 归还对象池。

### 3. 彻底告别 2D 手算 sortingOrder
- 不再使用容易穿帮的 `sortingOrder = 2000 - y`。
- 2D 特效贴花走 `KiHan/Render/SpriteZWriteAlphaTest` 深度写入 Shader；3D 粒子网格走标准渲染管线。

---

## 二、 动画数据结构改造 (AnimationFrameData & SpriteLibrary)

`AnimationFrameData` 严格保持纯粹性，专心负责 2D 序列帧步进、帧时长与物理 RootMotion 位移，不侵入任何业务事件逻辑（所有技能事件与判定由 ACT 动作层配置统一承载）：
- `AnimStep`: 仅包含 `FrameIndex`（切片库索引）、`Duration`（维持帧步数）、`RootMotion`（位移推力）。
- `TotalDurationTicks` 与 `TotalDurationSec`: 基于 30 FPS 动画时钟提供精确动画时间查询。

### 2. 判定盒合理尺寸与厚度预设
- 在 `CharacterSpriteLibrary.cs` 中，原本默认尺寸为 $0.5 \times 1.8$（米制单位，在立绘中只有不到 1 个像素），导致编辑器中盒子不可见。
- 现已全部校准为像素标准：
  - `DefaultHurtBox`: 宽 60px、高 140px、纵深厚度 80。
  - `DefaultHitBox`: 宽 60px、高 60px、纵深厚度 80。
  - `SidePresets`: 预设纵深厚度 `[50, 80, 100, 150]`。

---

## 三、 动画与切片帧编辑器 Bug 修复清单

### 1. 帧库编辑器 (`SpriteLibraryEditor.cs`) 修复
- **修复致命的双重坐标偏移 Bug**：
  - 原版在 `GUI.BeginGroup(rect)` 内部使用了带窗口绝对坐标的 `rect.center`，在 `HandleInput` 中再次叠加 `screenRect.position`，导致鼠标点击位置与实际绘制方框出现巨大偏移。
  - 现已重构坐标系映射，鼠标拖拽位置与 Sprite 像素实现 1:1 绝对对齐。
- **修复未标记 Dirty 导致修改丢失的 Bug**：
  - 原版在调整 `Offset`、绘制 `HurtBoxes`、`HitBoxes` 与锚点时没有调用 `EditorUtility.SetDirty`，重启 Unity 或切场景修改全部丢失。现已在所有交互改动后强制调用 `SetDirty`。
- **增加操作快捷提示条**：
  - 视口顶部清晰提示：`Shift+左键`画受击盒、`Shift+右键`画攻击盒、`Ctrl+左键`调立绘中心、`Alt+左键`调锚点、`滚轮`缩放、`中键/右键`平移。

### 2. 动画时间轴编辑器 (`AnimationEditor.cs`) 修复
- **修复预览播放帧率错误**：
  - 原版写死 `interval = 0.066f * duration`（15 FPS），导致动作预览严重慢放。现已修正为标准的逻辑帧基准 `LogicEntity.LOGIC_TICK_TIME * duration`（30 FPS 精确播放）。
- **增加预览视口判定盒覆盖显示**：
  - 预览播放时同步把该帧对应的受击盒（蓝）和攻击盒（红）实时绘制在预览立绘上，方便直观观察出招判定时间点。
- **支持非循环动画自动停止**：
  - 当 `IsLoop = false` 时，动画播放到末尾自动停在最后一帧，不会无限重新循环。
- **支持可视化编辑关键帧事件**：
  - 每个步骤（Step）可直观点击 `+ 添加事件`，配置对应的音效、特效名与依附挂点。
- **修复数组删除时的越界崩溃**：
  - 点击 `DEL` 删除步骤时，增加索引钳制保护，彻底根除 IndexOutOfRangeException。

---

## 四、 实操代码示例

### 播放一个 3D 纵深特效
```csharp
using KiHan.Logic;
using Managers;

// 1. 在受击处播放独立世界火花特效
EffectData hitEffect = new EffectData
{
    EffectName = "Spark1",
    WorldPos = target.pos,           // 水平与纵深坐标
    Height = target.height,          // 离地高度
    IsFacingLeft = target.IsFacingLeft,
    Offset = new Vector2(0, 30),
    BindEntity = null                // 独立世界特效
};
EffectManager.Instance.PlayEffect(hitEffect);

// 2. 在角色胸口挂接持续火焰特效
EffectData buffEffect = new EffectData
{
    EffectName = "FireBuff",
    BindEntity = target,             // 自动依附到 target 的 Sockets 挂点上
    IsFacingLeft = target.IsFacingLeft
};
int effectHandle = EffectManager.Instance.PlayEffect(buffEffect);
```
