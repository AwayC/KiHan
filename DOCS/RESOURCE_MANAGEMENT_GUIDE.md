# KiHan 资源组织与资产管理实战指南 (RESOURCE_MANAGEMENT_GUIDE)

> **适用范围**：KiHan 格斗游戏项目。指导美术资产（Sprite、3D粒子、网格）、表现层 Prefab（角色、投射物、分身、打击火花）、逻辑配置数据（Action.json）的规范化管理与全生命周期开发流程。

---

## 目录
- [一、 核心管理原则：逻辑与表现严格解耦](#一-核心管理原则逻辑与表现严格解耦)
- [二、 资源目录存放规范](#二-资源目录存放规范)
- [三、 核心 API 使用规范 (代码实操)](#三-核心-api-使用规范-代码实操)
  - [1. 资产加载与释放 (AssetManager)](#1-资产加载与释放-assetmanager)
  - [2. 实例池化与生命周期 (ObjectPoolManager)](#2-实例池化与生命周期-objectpoolmanager)
  - [3. 对局开局预热 (Prewarm) 与局内 0 IO](#3-对局开局预热-prewarm-与局内-0-io)
  - [4. 生命周期监听 (IPoolable) 与自动回收 (AutoRecycleEffect)](#4-生命周期监听-ipoolable-与自动回收-autorecycleeffect)
- [四、 新增一个格斗角色全流程实操示范 (以鸣人为例)](#四-新增一个格斗角色全流程实操示范-以鸣人为例)
  - [步骤 1：美术资源与图集制作](#步骤-1美术资源与图集制作)
  - [步骤 2：制作表现层 Prefab (NarutoView.prefab)](#步骤-2制作表现层-prefab-narutoviewprefab)
  - [步骤 3：配置动作与判定表 (naruto_actions.json)](#步骤-3配置动作与判定表-naruto_actionsjson)
  - [步骤 4：在战斗加载阶段进行开局预热 (Prewarm)](#步骤-4在战斗加载阶段进行开局预热-prewarm)
- [五、 新增技能生成物规范 (SimpleEntity vs Actor)](#五-新增技能生成物规范-simpleentity-vs-actor)
- [六、 战斗场景地图（2D / 3D）制作与接入全流程规范](#六-战斗场景地图2d--3d制作与接入全流程规范)
- [七、 为什么以后切 Addressables 零成本？](#七-为什么以后切-addressables-零成本)

---

## 一、 核心管理原则：逻辑与表现严格解耦

在帧同步对战中，必须遵循以下铁律：

```
                    【逻辑层 (Logic Layer)】
                    • 纯定点数、纯状态、确定性
                    • 严禁引用 GameObject, Transform, Sprite, AudioClip
                    • 招式表只声明资源标识串:
                      "animKey": "attack_1",
                      "fxId": "Effects/Hits/Spark1",
                      "spawnId": "Entities/Actors/NarutoClone"
                                  │
                                  ▼ (逻辑事件派发: ActionEvent)
                    【表现层 (View Layer)】
                    • EntityView, PlayerView 监听事件
                    • 通过 AssetManager 加载 / ObjectPoolManager 提取
                    • 渲染播放动画、生成粒子、播放音效
```

1. **逻辑配置不碰原生资产**：逻辑只记录地址/ID，无论资源实际如何移动、打包、热更，逻辑层永远无需重编。
2. **战斗对战中 0 动态分配**：一切对局可能用到的火花、特效、投射物、分身模型，必须在 Loading 界面预先生成进对象池。战斗过程中只进行 `Spawn()` 和 `Recycle()`。

---

## 二、 资源目录存放规范

推荐将所有美术资产、Prefab 与配置收敛管理（目前位于 `Assets/AssetPackages/Resources/`，未来可一键升级 Addressables）：

```text
Assets/AssetPackages/Resources/
├── Characters/                      # 角色专属资产包
│   └── 90001_Naruto/                # 按 CharacterId 隔离
│       ├── Config/
│       │   └── naruto_actions.json  # 动作与判定表
│       ├── View/
│       │   └── NarutoView.prefab    # 角色表现根预制体 (挂载 PlayerView)
│       └── Atlas/                   # 角色精灵图集
│           └── naruto_atlas.spriteatlas
│
├── Entities/                        # 技能生成物与召唤物
│   ├── SimpleEntities/              # 简单物理/投射物 (火球、手里剑、豪火球)
│   │   ├── Fireball.prefab          # 挂载 SimpleEntityView, AutoRecycleEffect
│   │   └── RasenganBall.prefab
│   └── Actors/                      # 具备完整动作控制器的分身/召唤兽
│       └── NarutoClone.prefab       # 挂载 ActionController, CloneBrain
│
├── Effects/                         # 全局通用打击火花与环境特效
│   ├── Hits/                        # 5 维打击反馈火花 (Spark1, Spark2, SparkHeavy)
│   │   ├── Spark1.prefab            # 挂载 AutoRecycleEffect (自动归还)
│   │   └── SparkHeavy.prefab
│   └── Scene/                       # 烟尘、地裂、落叶 (支持 3D 粒子系统)
│       └── Dust_Run.prefab
│
├── Config/
│   ├── CharacterRegistry.asset      # 角色注册表
│   └── MapRegistry.asset            # 地图注册表：登记顺序即选图顺序，第一张已启用的为开局默认地图
│
├── Maps/<id>/                       # 场景预制体 (scen.prefab 由 MapConfig.scene 引用)
│   └── scen.prefab                  # 2D 背景板预制体 或 3D 场景预制体 (Stage / Far 子节点)
│
└── UI/                              # UI (框架与规则见 DOCS/UI_FRAMEWORK.md)
    ├── Panels/<模块>/<类名>.prefab   # 面板 / 弹窗 (根节点挂同名 UIPanel 脚本，不带 Canvas)
    │   ├── Common/                  # ToastPanel / ConfirmPopup / LoadingPanel
    │   ├── Login/  Lobby/  BattleHud/
    ├── Number/                      # 图片数字 DigitFont (伤害跳字为世界空间 SpriteRenderer)
    ├── Widgets/                     # 被面板嵌套引用的通用预制体 (Slider、Loading)
    └── Shadow/                      # 角色脚下阴影

Assets/AssetPackages/Maps/<id>/      # 地图清单与美术资源 (不在 Resources 下，随预制体 / 注册表引用打包)
├── <id>.asset                       # MapConfig：id、displayName、scene、shot、is3D、camera3D (3D 相机参数)
├── shot.png                         # 选图截图 (320×443 Sprite)
└── Meshes/ Materials/ Textures/     # 3D 地图的网格、材质与贴图资产

新增地图：建 Maps/<id>/ 放截图 → Create/KiHan/Map Config 填写配置 (3D 地图勾选 is3D) → 登记到 MapRegistry。
全部地图共用 MapLogic 边界，局内选图通过 GameFlow.ChangeMap 走 LoadingPanel 过渡并替换场景 (MapManager.ChangeScene)。
```

---

## 三、 核心 API 使用规范 (代码实操)

### 1. 资产加载与释放 (`AssetManager`)
用于加载不频繁实例化的资源（如动作 JSON、图集、数据配置、音频）：

```csharp
using KiHan.Asset;

// 1. 同步加载 TextAsset / ScriptableObject (通常在初始化时)
TextAsset actionsJson = AssetManager.Instance.Load<TextAsset>("Characters/90001_Naruto/Config/naruto_actions");

// 2. 异步加载 (推荐方式)
AssetManager.Instance.LoadAsync<AudioClip>("Sounds/Hits/hit_punch", (clip) =>
{
    if (clip != null) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
});

// 3. 加载图集内所有 Sprites
Sprite[] allIcons = AssetManager.Instance.LoadAll<Sprite>("UI/SkillButton/90001_SkillIconAtlas");

// 4. 释放不需要的资源引用
AssetManager.Instance.Release("Characters/90001_Naruto/Config/naruto_actions");
```

---

### 2. 实例池化与生命周期 (`ObjectPoolManager`)
用于战斗中频繁出现和销毁的对象（打击火花、技能特效、投射物、伤害跳字）：

```csharp
using KiHan.Asset;

// 从池中提取打击火花 (若池空会自动从 AssetManager 加载并实例化)
GameObject sparkGo = ObjectPoolManager.Instance.Spawn(
    "Effects/Hits/Spark1", 
    worldPos, 
    Quaternion.identity, 
    parent: null, 
    category: PoolCategory.FX
);

// 归还对象 (无需传地址，系统通过内置的 PoolItemTracker 自动归还对应池)
ObjectPoolManager.Instance.Recycle(sparkGo);
```

---

### 3. 对局开局预热 (Prewarm) 与局内 0 IO
在选人结束、进入战斗 Loading 流程时（`BattleManager.LoadBattleRoutine`），进行批量预热：

```csharp
// 在加载界面协程中执行:
public IEnumerator PrewarmBattleAssets(int charId1, int charId2)
{
    // 预热打击火花: 提前在池中实例化 10 个 Spark1、5 个 SparkHeavy
    ObjectPoolManager.Instance.Prewarm("Effects/Hits/Spark1", 10, PoolCategory.FX);
    ObjectPoolManager.Instance.Prewarm("Effects/Hits/SparkHeavy", 5, PoolCategory.FX);

    // 预热伤害跳字: 提前实例化 15 个跳字节点
    ObjectPoolManager.Instance.Prewarm("UI/DamageText/DamageTextNode", 15, PoolCategory.UI);

    // 预热鸣人专属生成物 (如果是鸣人出战)
    ObjectPoolManager.Instance.Prewarm("Entities/Actors/NarutoClone", 2, PoolCategory.Entity);
    ObjectPoolManager.Instance.Prewarm("Entities/SimpleEntities/RasenganBall", 2, PoolCategory.Entity);

    yield return null; // 预热完成
}

// 退出战斗时清理战斗局部池，防止内存堆积:
public void OnExitBattle()
{
    ObjectPoolManager.Instance.ClearCategory(PoolCategory.FX);
    ObjectPoolManager.Instance.ClearCategory(PoolCategory.Entity);
    ObjectPoolManager.Instance.ClearCategory(PoolCategory.UI);
    AssetManager.Instance.UnloadUnused(); // 卸载无用图集
}
```

---

### 4. 生命周期监听 (`IPoolable`) 与自动回收 (`AutoRecycleEffect`)

#### 让预制体支持自动回收：
对于粒子特效（如打击火花），只需要在 Prefab 根节点上挂载 `AutoRecycleEffect` 组件：
- 如果设置了 `duration > 0`，倒计时到期自动回收；
- 如果 `duration = 0`，自动监听内部所有的 `ParticleSystem`，粒子播放完毕后自动执行 `Recycle`，**无需任何业务代码手动调用**。

#### 自定义组件实现 `IPoolable` 契约：
```csharp
public class DamageTextNode : MonoBehaviour, IPoolable
{
    public void OnSpawn()
    {
        // 从池中唤醒: 重置透明度、启动抛物线缓动动画
    }

    public void OnRecycle()
    {
        // 归还回池: 停止 DOTween 缓动、清空文本
    }
}
```

---

## 四、 新增一个格斗角色全流程实操示范 (以鸣人为例)

### 步骤 1：美术资源与图集制作
1. 将美术切好的 2D 序列帧 PNG 图片放入 `Assets/AssetPackages/Resources/Characters/90001_Naruto/Sprites/`。
2. 创建 SpriteAtlas（如 `naruto_atlas.spriteatlas`），把该角色图片打包，勾选 Tight Packing，材质开启深度支持。

### 步骤 2：制作表现层 Prefab (`NarutoView.prefab`)
在 Unity 场景中创建 GameObject，层级结构如下：
```text
NarutoView (根节点，挂载 PlayerView、SortingGroup)
├── DisplayRoot (插值显示根，坐标由 LogicEntity.height 驱动)
│   ├── MainRenderer (SpriteRenderer，材质使用 Alpha-Test ZWrite Shader)
│   └── Sockets (骨骼挂点，规范命名)
│       ├── Socket_Head
│       ├── Socket_Chest     # 默认受击火花挂接点
│       ├── Socket_Hand_L
│       ├── Socket_Hand_R    # 螺旋丸特效挂接点
│       └── Socket_Root      # 脚底阴影与地面烟尘挂接点
└── ShadowQuad (贴地阴影 Quad，Y 恒定 0.01)
```
保存为预制体放置在 `Characters/90001_Naruto/View/NarutoView.prefab`。

### 步骤 3：配置动作与判定表 (`naruto_actions.json`)
在 `Characters/90001_Naruto/Config/naruto_actions.json` 中配置动作与判定：
```json
{
  "actions": [
    {
      "id": "attack1",
      "animKey": "Attack_1",
      "durationTicks": 15,
      "autoNextActionId": "idle",
      "cancelTag": ["attack1", "attack"],
      "beCancelledTag": [
        {
          "startTick": 7,
          "endTick": 15,
          "allowTags": ["attack2", "skill1", "skill2"],
          "priority": 10
        }
      ],
      "attacks": [
        {
          "phase": 1,
          "damage": 50,
          "hitStun": 12,
          "pushPower": { "distanceX": 0.8, "durationTicks": 8, "tween": "QuadOut" },
          "hitEffectName": "Effects/Hits/Spark1",
          "hitEffectSocket": "Socket_Chest"
        }
      ]
    }
  ]
}
```

### 步骤 4：在战斗加载阶段进行开局预热 (Prewarm)
在 `BattleManager` 加载对局时：
```csharp
ObjectPoolManager.Instance.Prewarm("Characters/90001_Naruto/View/NarutoView", 2, PoolCategory.Entity);
ObjectPoolManager.Instance.Prewarm("Effects/Hits/Spark1", 10, PoolCategory.FX);
```

---

## 五、 新增技能生成物规范 (SimpleEntity vs Actor)

根据 `EntityController.md` 规范，技能生成物必须根据复杂度分流：

| 分类 | 典型示例 | Prefab 内部构成 | 驱动方式 |
| :--- | :--- | :--- | :--- |
| **SimpleEntity** (简单实体) | 豪火球、苦无、手里剑、风魔手里剑、地刺陷阱 | `SimpleEntityView` + 粒子/Sprite + 碰撞体 + `AutoRecycleEffect` | 逻辑层仅计算固定匀速/抛物线位移，命中后派发事件并归还池中。**无动作状态机**。 |
| **Actor** (复杂角色实体) | 鸣人影分身、木叶丸、佐助雷分身、通灵大蛤蟆 | 结构与角色完全相同：`ActionController` + `CloneBrain` + `SpawnedActorLifecycle` | 拥有完整出招、受击、被击飞逻辑，由 `CloneBrain` 注入指令驱动。 |

---

## 六、 战斗场景地图（2D / 3D）制作与接入全流程规范

项目同时支持 **传统 2D 分层视差地图**（如 `01 春日街道`、`02 中忍考试会场`、`03 决斗场`）与 **3D 透视视差地图**（如 `04 方格训练室(3D)`、`05 罗格镇(3D)`）。所有地图共用同一套定点数逻辑边界 `MapLogic`，表现层由 `MapManager` 与 `CameraControllor` 自动切换渲染模式。

### 1. 空间坐标与物理标尺规范 (`1 Unity 单位 = 1 米`)

| 空间维度 | 强制规范值 | 对齐说明 |
| :--- | :--- | :--- |
| **地台水平面 ($Y$)** | **$Y = 0$** | 角色脚底在 $Z = 0$ 处对应世界 $Y = 0$；3D 可走地台表面须严格保持在 $Y = 0$ 平面 |
| **场地中心与出生点** | 中心 $(0, 0, 0)$；1P $(-2, 0, 0)$；2P $(+2, 0, 0)$ | 与 `BattleManager` 出生坐标严格对齐 |
| **横向边界 ($X$)** | 可走范围 **$X \in [-8.0\text{m},\; +8.0\text{m}]$**<br>模型外观延伸至 **$X \in [-16\text{m},\; +16\text{m}]$** | 对应 `MapLogic.MinX / MaxX = ±8`；左右额外延伸确保相机跟随至版边、震屏或旋转（$\pm 30^\circ$）时不露底 |
| **纵深边界 ($Z$)** | 逻辑纵深 $y$ 对应地面 $Z = \dfrac{D\,y}{0.368\,D - y}$（$D$ 为地台相机物距）<br>$D = 32$ 时可走范围约 **$Z \in [-2.5\text{m},\; +3.0\text{m}]$** | 对应 `MapLogic.MinY / MaxY = ±1.009371`；后沿护栏 / 背景墙底边放在 `MaxY` 对应的 $Z$ 处，角色走到最上方时脚尖正好抵住 |
| **可走区净空铁律** | 可走区域上方及相机正前方 ($Z < -3\text{m}$) **严禁放高大遮挡物** | 角色由最顶层正交相机绘制，场地内不得摆放与角色穿插的 3D 障碍物；所有建筑只能放在后沿护栏外 ($Z > +3.0\text{m}$) 或左右版边外 ($|X| > 8.2\text{m}$) |

### 2. 三层相机渲染架构与 Layer 拆分规范（3D 地图专属）

3D 地图（`MapConfig.is3D = true`）不挂载 `ParallaxEffect`，也不用脚本在运行时生成几何：网格、材质、贴图都是资产，预制体可直接编辑保存。视差由 `CameraControllor` 持有的 `Map3DCameraRig` 两台透视相机与正交主相机合成：

1. **远景相机 (`BgFar3DCamera`, depth = 主相机 - 2, `SolidColor`, `Layer 9: Map3DFar`)**：天穹、云层、下层广场与远景建筑。物距、机位高度、俯仰、横向视差倍率、上下偏移、视野倍率、底色都可在 `MapConfig.camera3D` 中按地图调节。
2. **地台相机 (`BgStage3DCamera`, depth = 主相机 - 1, 只清深度, `Layer 8: Map3D`)**：脚下可走地台、后沿护栏以及**与地台直接接壤的左右近景侧楼**。开放物距 `stageDistance`（越大地砖近大远小越弱）、机位高度斜率 `stageHeightSlope`（越大地面纵向铺得越开）、俯仰 `stagePitch`（竖直物体上下汇聚），地台在画面中的宽度始终不变。
   - **接壤铁律**：墙根直接立在地台上的近景侧楼必须与地台同属 `Layer 8`，否则两台相机物距不同会让墙根与地面相对滑动。
3. **正交主相机 (`MainCamera`, depth 0, 只清深度, 剔除 `Layer 8 & 9`)**：2D 角色精灵、脚底阴影、打击火花与伤害跳字。

> **移轴对齐**：两台相机都按「地面纵深中线」($Y = 0, Z = 0$ 的直线) 求视野与投影矩阵移轴 `m02 / m12`：这条线在任何俯仰下都与画面横轴平行，令其横向比例等于正交、参考点落在正交主相机的同一屏幕位置——地台中线与正交角色**逐像素同步（零滑步）**，物距 / 高度 / 俯仰怎么调都成立。地台的缩放与平移会破坏该对齐，因此不开放。

### 3. 3D 材质与渲染队列 (RenderQueue) 规范

| 材质类别 | 推荐 Shader | 深度与裁剪设置 | `RenderQueue` |
| :--- | :--- | :--- | :---: |
| **天空穹顶 (Sky Dome)** | `KiHan/Map/Lightmapped`（不开光照贴图） | **`ZWrite Off`**（严禁写深度，防遮挡远楼与云层） | `1000` (Background) |
| **不透明地面与建筑** | `KiHan/Map/Lightmapped` | `ZWrite On`, `Cull Back`；有烘焙光照时开 `_UseLightmap`，光照贴图坐标放网格 UV2 | `2000` (Geometry) |
| **镂空树叶 / 栏杆 / 人群** | `KiHan/Map/Lightmapped` | `ZWrite On`, `_Cutoff = 0.33` | `2450` (AlphaTest) |
| **半透明云层 / 雾气** | `KiHan/Map/Lightmapped` | `ZWrite Off`, `_SrcBlend = SrcAlpha`, `_DstBlend = OneMinusSrcAlpha` | `2900` (Transparent) |

> **光照**：场景不用实时灯光（2D 角色不受光，混用会割裂）。有原始烘焙光照贴图的模型（如 UE 导出的 `LQ_Lightmap_*`）直接接入：线性光照 `L = 0.98 × (lm² × Scale + Add)`，项目为 Gamma 色彩空间，输出前乘 `L^(1/2.2)`；色调 `_Tint` 同样存 Gamma 值。整体太亮 / 太暗调材质 `_Exposure`。没有原始光照贴图的简单场景（如 04 训练室）自绘光照贴图：平面网格的 UV2 铺满整面，贴图里画灯池、墙面洗墙光、墙角接触阴影与四周压暗，材质 `Scale = 1, Add = 0`（贴图值约等于输出亮度），导入时关闭 sRGB。

### 4. 新增一张地图的 4 步标准流程

1. **准备资源**：
   - 在 `Assets/AssetPackages/Maps/<id>/` 下放入选图缩略图 `shot.png`（`320×443`，`TextureType = Sprite`），3D 地图的网格 / 材质 / 贴图放 `Meshes/`、`Materials/`、`Textures/`。
   - 若从外部引擎（UE / Blender glTF）导入 3D 模型，须将右手系顶点 `(x, y, z)` 转换为 Unity 左手系 `(x, y, -z)`、翻转三角面绕序 `(i0, i2, i1)`，并将贴图 UV 纵向翻转 `v_unity = 1 - v_gltf`。
2. **制作场景预制体 (`Assets/AssetPackages/Resources/Maps/<id>/scen.prefab`)**：
   - **2D 地图**：各图层挂载 `SpriteRenderer` 与 `ParallaxEffect`。
   - **3D 地图**：根节点在 `(0, 0, 0)`，分 `Stage`（`Layer 8`）与 `Far`（`Layer 9`）两组子节点；每个模型一个 GameObject (MeshFilter + MeshRenderer)，枢轴在模型包围盒中心，可直接移动、改图层、换材质并保存。场地范围参考网格可放在 `EditorOnly` 标签的隐藏子节点里（构建时剔除）。
3. **创建地图配置 (`Assets/AssetPackages/Maps/<id>/<id>.asset`)**：
   - 右键 `Create -> KiHan -> Map Config`，填写 `id`、`displayName`、`scene`、`shot`；**3D 地图勾选 `is3D`**，并在 `camera3D` 中调节该场景的相机参数（地台：`stageDistance`、`stageHeightSlope`、`stagePitch`；远景：`farDistance`、`farHeightSlope`、`farPitch`、`farParallaxScale`、`farVerticalOffset`、`farZoomScale`、`farBackgroundColor`）。Play 模式下直接修改该资源，或在 Main Camera 的 Inspector 底部「当前地图 3D 相机参数」中修改，即时生效，退出后数值保留。
4. **登记到注册表 (`Assets/AssetPackages/Resources/Config/MapRegistry.asset`)**：
   - 在 `MapRegistry.asset` 的 `maps` 列表中新增一项，拖入 `<id>.asset` 并勾选 `enabled`。局内通过 HUD「场景」按钮打开 `MapSelectPopup`，经 `GameFlow.ChangeMap` 在 Loading 遮罩下切换。

---

## 七、 为什么以后切 Addressables 零成本？

因为业务代码中**没有任何一行直接硬编码写 `Resources.Load`**，所有调用全部经由 `AssetManager.Instance`：
- 当前底层使用的是 `ResourcesAssetProvider`。
- 后续如果要接入 Addressables，只需要编写一个 `AddressablesAssetProvider : IAssetProvider`，并在游戏启动时执行：
  ```csharp
  AssetManager.Instance.SetProvider(new AddressablesAssetProvider());
  ```
- **上层所有角色动作、判定盒、特效生成、UI 界面代码完全不需要改动任何一行代码！**
