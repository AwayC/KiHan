using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KiHan.Config;
using KiHan.Logic;

namespace KiHan.Editor
{
    /// <summary>
    /// 导入 Tools/SpriteFrameExporter 或 Reverse/tools/export_character.py 的导出结果 (每个动作一个目录：animation.json + 图片) 到角色包 Assets/AssetPackages/Characters/{角色}/：
    /// - 图片复制到 {角色}/{动作}/ (文件名带子目录时相对导出根目录，如 sprites/x.png → {角色}/sprites/x.png，多个动作共用)，按项目约定导入 (Sprite、中心 pivot、FullRect)
    /// - 帧写入包内已有的帧库 (没有时新建 Anims/{角色}FrameLib.asset)，Offset = 图片中心相对脚底的像素偏移；file 为空的帧不显示本体
    /// - 每个动作生成 Anims/{动作}.asset (AnimationFrameData，资源名即 animKey)；帧带 duration (>0) 时作为步骤时长
    /// - 包内没有清单时新建 Character.asset 并刷新 (之后在清单上填写 ID、名字、头像并登记到注册表)
    /// 重复导入按帧名匹配：替换 Sprite 与 Offset；replaceFrameData 为 false 时保留已编辑的判定盒、锚点、额外图层和步骤时长，
    /// 为 true 时 (原版角色转换) 判定盒与额外图层以 JSON 为准。
    /// 外部工具可写入 Temp/SequenceFrameImport.request (第一行导出目录，第二行角色目录名)，编辑器下次加载脚本时自动导入一次。
    /// </summary>
    public class SequenceFrameImporter : EditorWindow
    {
        [Serializable]
        private class LayerJson
        {
            public string file;
            public int width;
            public int height;
            public float pivotX; // 脚底在图片中的位置，像素，原点左上
            public float pivotY;
            public int order = 1; // EffectLayerInfo.OrderOffset，越大越靠前
        }

        [Serializable]
        private class FrameJson
        {
            public string file; // 为空表示该帧不显示本体
            public string name;
            public int width;
            public int height;
            public float pivotX; // 脚底在图片中的位置，像素，原点左上
            public float pivotY;
            public int duration; // 步骤时长 (逻辑帧)，0 表示沿用已有时长或 1
            public LogicBox[] hurtBoxes;
            public LogicBox[] hitBoxes;
            public LayerJson[] layers;
        }

        [Serializable]
        private class AnimationJson
        {
            public string name;
            public float pixelsPerUnit;
            public bool replaceFrameData;
            public FrameJson[] frames;
        }

        private const string SpriteRoot = "Assets/AssetPackages/Characters";
        private const string AnimFolder = "Anims";

        /// <summary>本次导入中的图片路径 -> pixelsPerUnit，供 OnPreprocessTexture 设置导入参数</summary>
        internal static readonly Dictionary<string, float> PendingTextures = new Dictionary<string, float>();

        private string _exportDir = "";
        private string _character = "";

        [MenuItem("KiHan/资源/导入序列帧 (SpriteFrameExporter)")]
        private static void Open()
        {
            GetWindow<SequenceFrameImporter>("导入序列帧");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "选择导出目录 (包含若干动作子目录，每个子目录有 animation.json)。\n" +
                "重复导入替换图片与对齐偏移；已编辑的判定盒、锚点、额外图层会保留 (replaceFrameData 的导出除外)，帧带 duration 时覆盖步骤时长。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _exportDir = EditorGUILayout.TextField("导出目录", _exportDir);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string picked = EditorUtility.OpenFolderPanel("选择 SpriteFrameExporter 导出目录", _exportDir, "");
                    if (!string.IsNullOrEmpty(picked)) _exportDir = picked;
                }
            }
            _character = EditorGUILayout.TextField("角色目录名", _character);

            string character = _character.Trim();
            EditorGUILayout.LabelField("图片", $"{SpriteRoot}/{character}/<动作>/");
            EditorGUILayout.LabelField("数据", $"{SpriteRoot}/{character}/{AnimFolder}/<动作>.asset、帧库、{CharacterManifest.FileName}.asset");

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_exportDir) || string.IsNullOrWhiteSpace(character)))
            {
                if (GUILayout.Button("导入", GUILayout.Height(28)))
                {
                    Import(_exportDir, character);
                }
            }
        }

        /// <summary>
        /// 命令行入口：
        /// Unity -batchmode -quit -projectPath . -executeMethod KiHan.Editor.SequenceFrameImporter.ImportFromCommandLine
        ///       -sfeInput &lt;导出目录&gt; -sfeCharacter &lt;角色目录名&gt;
        /// </summary>
        public static void ImportFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string key)
            {
                int i = Array.IndexOf(args, key);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }

            string input = Arg("-sfeInput");
            string character = Arg("-sfeCharacter");
            if (input == null || character == null)
            {
                throw new ArgumentException("需要参数 -sfeInput <导出目录> -sfeCharacter <角色目录名>");
            }
            if (!Import(input, character))
            {
                EditorApplication.Exit(1);
            }
        }

        public static bool Import(string exportDir, string character)
        {
            List<(string dir, AnimationJson anim)> anims = LoadAnimations(exportDir);
            if (anims.Count == 0)
            {
                Debug.LogError($"[SequenceFrameImporter] {exportDir} 下没有找到 animation.json");
                return false;
            }

            string spriteDir = $"{SpriteRoot}/{character}";
            string dataDir = $"{spriteDir}/{AnimFolder}";
            Directory.CreateDirectory(spriteDir);
            Directory.CreateDirectory(dataDir);

            // 1. 复制图片并按项目约定导入 (多个动作共用的图片只复制一次)
            var copied = new HashSet<string>();
            PendingTextures.Clear();
            foreach ((string dir, AnimationJson anim) in anims)
            {
                float ppu = anim.pixelsPerUnit > 0 ? anim.pixelsPerUnit : GameConfig.PIXELS_PER_UNIT;
                foreach (FrameJson f in anim.frames)
                {
                    CopyImage(exportDir, dir, spriteDir, f.file, ppu, copied);
                    if (f.layers == null) continue;
                    foreach (LayerJson layer in f.layers) CopyImage(exportDir, dir, spriteDir, layer.file, ppu, copied);
                }
            }
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                PendingTextures.Clear();
            }

            // 2. 帧库：沿用包内已有的帧库 (可能已改名)，按帧名匹配已有帧
            string libGuid = AssetDatabase.FindAssets("t:" + nameof(CharacterSpriteLibrary), new[] { spriteDir }).FirstOrDefault();
            CharacterSpriteLibrary lib = libGuid != null ? AssetDatabase.LoadAssetAtPath<CharacterSpriteLibrary>(AssetDatabase.GUIDToAssetPath(libGuid)) : null;
            if (lib == null)
            {
                lib = CreateInstance<CharacterSpriteLibrary>();
                AssetDatabase.CreateAsset(lib, $"{dataDir}/{character}FrameLib.asset");
            }
            var frameIndex = new Dictionary<string, int>();
            for (int i = 0; i < lib.AllFrames.Count; i++)
            {
                string frameName = lib.AllFrames[i].Name;
                if (frameName != null && !frameIndex.ContainsKey(frameName)) frameIndex[frameName] = i;
            }

            int added = 0, updated = 0;
            foreach (var (dir, anim) in anims)
            {
                foreach (FrameJson f in anim.frames)
                {
                    Sprite sprite = null;
                    if (!string.IsNullOrEmpty(f.file) && !TryLoadSprite(exportDir, dir, spriteDir, f.file, out sprite)) continue;

                    if (frameIndex.TryGetValue(f.name, out int idx))
                    {
                        updated++;
                    }
                    else
                    {
                        idx = lib.AllFrames.Count;
                        frameIndex[f.name] = idx;
                        lib.AllFrames.Add(new SpriteFrameData { Name = f.name, HurtBoxes = new List<LogicBox>(), HitBoxes = new List<LogicBox>() });
                        added++;
                    }

                    SpriteFrameData frame = lib.AllFrames[idx];
                    frame.Sprite = sprite;
                    frame.Offset = sprite != null ? CenterOffset(f.width, f.height, f.pivotX, f.pivotY) : Vector2.zero;
                    if (!anim.replaceFrameData) continue;

                    frame.HurtBoxes = new List<LogicBox>(f.hurtBoxes ?? Array.Empty<LogicBox>());
                    frame.HitBoxes = new List<LogicBox>(f.hitBoxes ?? Array.Empty<LogicBox>());
                    frame.ExtraLayers = new List<EffectLayerInfo>();
                    foreach (LayerJson layer in f.layers ?? Array.Empty<LayerJson>())
                    {
                        if (!TryLoadSprite(exportDir, dir, spriteDir, layer.file, out Sprite layerSprite)) continue;
                        // 额外图层挂在主精灵节点下 (ActorView / 各编辑器预览均按 主精灵 Offset + 图层 Offset 绘制)，
                        // 所以图层 Offset 是相对主精灵中心的偏移，而 JSON 的 pivot 是相对脚底
                        frame.ExtraLayers.Add(new EffectLayerInfo
                        {
                            Sprite = layerSprite,
                            Offset = CenterOffset(layer.width, layer.height, layer.pivotX, layer.pivotY) - frame.Offset,
                            OrderOffset = layer.order,
                        });
                    }
                }
            }
            EditorUtility.SetDirty(lib);

            // 3. 每个动作一个 AnimationFrameData，资源名即 animKey
            foreach ((string dir, AnimationJson anim) in anims)
            {
                string animKey = Path.GetFileName(dir);
                string animPath = $"{dataDir}/{animKey}.asset";
                var data = AssetDatabase.LoadAssetAtPath<AnimationFrameData>(animPath);
                if (data == null)
                {
                    data = CreateInstance<AnimationFrameData>();
                    AssetDatabase.CreateAsset(data, animPath);
                }

                var oldDurations = new Dictionary<string, int>();
                if (data.Library != null)
                {
                    foreach (AnimationFrameData.AnimStep step in data.Steps)
                    {
                        SpriteFrameData old = data.Library.GetFrame(step.FrameIndex);
                        if (old?.Name != null && !oldDurations.ContainsKey(old.Name)) oldDurations[old.Name] = step.Duration;
                    }
                }

                data.AnimName = animKey;
                data.Library = lib;
                data.Steps = anim.frames
                    .Where(f => frameIndex.ContainsKey(f.name))
                    .Select(f => new AnimationFrameData.AnimStep
                    {
                        FrameIndex = frameIndex[f.name],
                        Duration = f.duration > 0 ? f.duration : oldDurations.TryGetValue(f.name, out int d) ? d : 1,
                    })
                    .ToList();
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();

            // 4. 角色包清单：没有时新建，并按目录收集动画与资源
            string manifestPath = CharacterPackages.FindManifestPath($"{spriteDir}/x");
            var manifest = manifestPath != null && Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') == spriteDir
                ? AssetDatabase.LoadAssetAtPath<CharacterManifest>(manifestPath)
                : null;
            if (manifest == null)
            {
                manifest = CreateInstance<CharacterManifest>();
                manifest.displayName = character;
                AssetDatabase.CreateAsset(manifest, $"{spriteDir}/{CharacterManifest.FileName}.asset");
            }
            CharacterPackages.Refresh(manifest);

            Debug.Log($"[SequenceFrameImporter] {character}: {anims.Count} 个动作，新增 {added} 帧，更新 {updated} 帧 -> {spriteDir}");
            return true;
        }

        private static List<(string dir, AnimationJson anim)> LoadAnimations(string exportDir)
        {
            var result = new List<(string, AnimationJson)>();
            if (!Directory.Exists(exportDir)) return result;

            IEnumerable<string> dirs = File.Exists(Path.Combine(exportDir, "animation.json"))
                ? new[] { exportDir }
                : Directory.GetDirectories(exportDir).OrderBy(d => d, StringComparer.Ordinal);

            foreach (string dir in dirs)
            {
                string jsonPath = Path.Combine(dir, "animation.json");
                if (!File.Exists(jsonPath)) continue;
                var anim = JsonUtility.FromJson<AnimationJson>(File.ReadAllText(jsonPath));
                if (anim?.frames == null || anim.frames.Length == 0)
                {
                    Debug.LogWarning($"[SequenceFrameImporter] 跳过空动作: {jsonPath}");
                    continue;
                }
                result.Add((dir, anim));
            }
            return result;
        }

        /// <summary>
        /// 导出图片路径 → 角色包内路径：文件名带子目录 (如 sprites/x.png) 时相对导出根目录，复制到角色目录下同一相对路径；否则相对动作目录
        /// </summary>
        private static (string src, string dst) ImagePaths(string exportDir, string animDir, string spriteDir, string file)
        {
            return file.Contains('/')
                ? (Path.Combine(exportDir, file), $"{spriteDir}/{file}")
                : (Path.Combine(animDir, file), $"{spriteDir}/{Path.GetFileName(animDir)}/{file}");
        }

        private static void CopyImage(string exportDir, string animDir, string spriteDir, string file, float ppu, HashSet<string> copied)
        {
            if (string.IsNullOrEmpty(file)) return;
            (string src, string dst) = ImagePaths(exportDir, animDir, spriteDir, file);
            if (!copied.Add(dst)) return;
            if (!File.Exists(src))
            {
                Debug.LogError($"[SequenceFrameImporter] 缺少图片: {src}");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
            PendingTextures[dst] = ppu;
        }

        private static bool TryLoadSprite(string exportDir, string animDir, string spriteDir, string file, out Sprite sprite)
        {
            string path = ImagePaths(exportDir, animDir, spriteDir, file).dst;
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogError($"[SequenceFrameImporter] {path} 没有导入为 Sprite");
            return sprite != null;
        }

        /// <summary>
        /// Sprite 为中心 pivot，渲染位置 = 脚底 + Offset (y 向上)
        /// </summary>
        private static Vector2 CenterOffset(int width, int height, float pivotX, float pivotY)
        {
            return new Vector2(width * 0.5f - pivotX, pivotY - height * 0.5f);
        }

        private const string RequestPath = "Temp/SequenceFrameImport.request";

        /// <summary>
        /// 外部工具请求的导入 (见类注释)：脚本加载后执行一次并删除请求文件
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RunPendingRequest()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestPath)) return;
                string[] lines = File.ReadAllLines(RequestPath);
                File.Delete(RequestPath);
                if (lines.Length < 2)
                {
                    Debug.LogError($"[SequenceFrameImporter] {RequestPath} 需要两行：导出目录、角色目录名");
                    return;
                }
                Import(lines[0].Trim(), lines[1].Trim());
            };
        }
    }

    /// <summary>序列帧图片的导入参数，与现有角色切片一致：Sprite、中心 pivot、FullRect、无 mipmap</summary>
    public class SequenceFrameTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!SequenceFrameImporter.PendingTextures.TryGetValue(assetPath, out float ppu)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
