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
- [六、 为什么以后切 Addressables 零成本？](#六-为什么以后切-addressables-零成本)

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
├── Maps/                            # 场景地图
│   └── Map_01/
│       ├── MapConfig.asset          # 逻辑边界与碰撞阻挡
│       └── Map_01.prefab            # 地面、多层视差背景、3D 场景模型
│
└── UI/                              # 战斗与通用 UI
    ├── BattleUI/
    │   └── Canvas.prefab
    └── DamageText/
        └── DamageTextNode.prefab    # 伤害跳字
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

## 六、 为什么以后切 Addressables 零成本？

因为业务代码中**没有任何一行直接硬编码写 `Resources.Load`**，所有调用全部经由 `AssetManager.Instance`：
- 当前底层使用的是 `ResourcesAssetProvider`。
- 后续如果要接入 Addressables，只需要编写一个 `AddressablesAssetProvider : IAssetProvider`，并在游戏启动时执行：
  ```csharp
  AssetManager.Instance.SetProvider(new AddressablesAssetProvider());
  ```
- **上层所有角色动作、判定盒、特效生成、UI 界面代码完全不需要改动任何一行代码！**
