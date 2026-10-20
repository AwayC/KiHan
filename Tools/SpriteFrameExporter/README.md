# SpriteFrameExporter

把 AssetStudio 导出的 Sprite Dump + Texture2D 还原成**原始像素**的序列帧 PNG（每个像素都是从图集里原样拷出来的，不做缩放）。

替代 `任意分辨率` 拼图工具（`pic.exe`）。旧工具是用相机截屏导出的，结果受屏幕分辨率影响。

## 输入

由 AssetStudio 从 `*.assetbundle` 导出的一个目录：

```
900081/
  assets.xml        # AssetStudio "Export list to XML"，用来把 Texture2D 的 PathID 对应到图集名
  Sprite/*.txt      # Sprite 对象 Dump（Export -> Dump）
  Texture2D/*.png   # 图集 PNG（Export -> Convert）
```

旧工具的 `pic_Data` 布局（`xml/assets.xml`、`txt/`、`png/`）也能直接用。

## 用法

```
# 图形界面：加载 -> 预览（红色十字 = pivot）-> 导出
SpriteFrameExporter.exe

# 命令行
SpriteFrameExporter.exe <输入目录> [-o 输出目录] [--mode anim|global|frame] [--padding N] [--filter 关键字]
```

| mode | 画布 |
|---|---|
| `anim`（默认） | 同一个动作的所有帧用同一块画布，按 pivot 对齐，可以直接当序列帧播放 |
| `global` | 所有动作共用一块画布 |
| `frame` | 每帧按内容裁到最小，pivot 写进 JSON |

输出为 `<输出目录>/<动作名>/<帧名>.png` 加上 `animation.json`（含每帧的 pivot 像素坐标，原点在左上角，以及 Unity 归一化 pivot 与 pixelsPerUnit），在 Unity 里重新切 Sprite 时可以直接用。

构建：`dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o bin/publish`（需要 .NET 8 Desktop Runtime），产物为 `bin/publish/SpriteFrameExporter.exe`。

## 导入游戏工程

Unity 菜单 `KiHan/资源/导入序列帧 (SpriteFrameExporter)`（`Assets/Editor/SequenceFrameImporter.cs`），选择导出目录、填写角色目录名，推荐使用 `--mode frame` 导出的结果：

- 图片复制到 `Assets/AssetPackages/Characters/<角色>/<动作>/`，导入为 Sprite、中心 pivot、FullRect，与现有角色一致。
- 帧写入 `Assets/AssetPackages/Resources/Characters/<角色>/frameLib.asset`，`Offset` 由 JSON 中的 pivot 算出（图片中心相对脚底），不需要手调。
- 每个动作生成 `<动作>.asset`（`AnimationFrameData`，资源名即 animKey），步骤时长默认 1。
- 重复导入按帧名匹配：只替换图片和 `Offset`，已画好的判定盒、锚点、额外图层以及步骤时长都保留。

命令行（Unity 未打开工程时）：

```
Unity.exe -batchmode -quit -projectPath <工程> -executeMethod KiHan.Editor.SequenceFrameImporter.ImportFromCommandLine -sfeInput <导出目录> -sfeCharacter <角色目录名>
```

## 原理

### 游戏的资源结构

角色的每一帧在打包时被切成几个不重叠的拼块（`xxx_0036@89_A/_B/_C`），分别塞进 `90081PackN` 图集。每个拼块是一个独立的 Unity `Sprite`：

- `m_Rect`：拼块在图集里的像素矩形（Unity 坐标系，左下角为原点）。
- `m_Pivot`：pivot，按 `m_Rect` 尺寸归一化。同一帧的所有拼块共用**同一个 pivot 点**（角色的原点），所以这个值经常远超出 0..1（例如 `-1.2`）。
- `m_RD.texture.m_PathID`：拼块所在的图集，通过 `assets.xml` 映射到具体的 PNG。
- `m_RD.textureRect` / `textureRectOffset`：图集里实际存储的像素及其在 `m_Rect` 中的位置（未裁剪时与 `m_Rect` 相同）。

游戏运行时把同一帧的几个拼块放在同一个 Transform 下渲染，它们的 pivot 重合，就拼回了完整的画面。

### 旧工具的做法

反编译 `pic_BackUpThisFolder_ButDontShipItWithYourGame/Managed/Assembly-CSharp.dll`（`ab包.png异步加载` / `LoadTexture` / `SignatureSave`）可以看到：

1. 按固定行号读取 txt（第 17、18 行是 pivot，第 3~6 行是 rect，第 37 行是贴图 PathID）。
2. 调用 `Sprite.Create(texture, rect, pivot, 100)` 生成 Sprite，按名字把 A/B/C 分到一组，放到同一位置的 4 个 `SpriteRenderer` 上。
3. 用 `captureCamera.Render()` 渲染到 RenderTexture，再用 `ReadPixels(截取框)` 截图保存。

所以输出像素取决于相机的 orthographicSize 和窗口分辨率（`orthographicSize = 5 * 1920 / Screen.width`），渲染过程中有一次采样和缩放，得不到原始像素。

### 本工具的做法

不经过渲染，直接在像素空间拼合：

1. 每个拼块左下角相对 pivot 的位置为
   `left = -pivot.x * rect.w + textureRectOffset.x`，`bottom = -pivot.y * rect.h + textureRectOffset.y`。
2. 同一帧的拼块共用 pivot，它们的 `left/bottom` 小数部分相同（导出数据中误差 < 0.002 px）。先减去这个公共小数部分再取整，拼块之间的相对位置就是精确的整数；剩下的亚像素偏移作为该帧的 pivot 写进 JSON。
3. 从图集 PNG 中拷贝 `textureRect`（AssetStudio 导出的 PNG 是从上到下存储的，所以 `y_png = texH - rect.y - rect.h`），放到画布上。拼块之间互不重叠，叠放顺序不影响结果。
4. 画布取帧（或动作、全局）所有拼块包围盒的并集，各帧以 pivot 对齐。

已在 `900081` 上验证：836 个 Sprite → 281 帧 / 26 个动作。逐帧与独立的 numpy 参考实现逐像素比较，281/281 完全一致。

### 限制

- 依赖 `assets.xml` 来找贴图；不在其中的 PathID 会被跳过，并按图集汇总报告。
- 不支持 SpriteAtlas 旋转打包（`settingsRaw` 带 packingRotation 的拼块），遇到时会跳过并提示。这个游戏的数据中没有这种情况。
- 同名 Sprite（AssetStudio 输出 `name #123.txt`）只保留不带 `#` 的那份。
