using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KiHan.Logic;

namespace KiHan.Editor
{
    /// <summary>
    /// 导入 Tools/SpriteFrameExporter 的导出结果 (每个动作一个目录：*.png + animation.json)：
    /// - 图片复制到 Assets/AssetPackages/Characters/{角色}/{动作}/，按项目约定导入 (Sprite、中心 pivot、FullRect)
    /// - 帧写入 Resources/Characters/{角色}/frameLib.asset，Offset = 图片中心相对脚底的像素偏移
    /// - 每个动作生成 Resources/Characters/{角色}/{动作}.asset (AnimationFrameData，资源名即 animKey)
    /// 重复导入按帧名匹配：只替换 Sprite 与 Offset，保留已编辑的判定盒、锚点、额外图层和步骤时长。
    /// </summary>
    public class SequenceFrameImporter : EditorWindow
    {
        [Serializable]
        private class FrameJson
        {
            public string file;
            public string name;
            public int width;
            public int height;
            public float pivotX; // 脚底在图片中的位置，像素，原点左上
            public float pivotY;
        }

        [Serializable]
        private class AnimationJson
        {
            public string name;
            public float pixelsPerUnit;
            public FrameJson[] frames;
        }

        private const string SpriteRoot = "Assets/AssetPackages/Characters";
        private const string DataRoot = "Assets/AssetPackages/Resources/Characters";

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
                "选择 SpriteFrameExporter 的输出目录 (包含若干动作子目录，每个子目录有 animation.json)。\n" +
                "重复导入只替换图片与对齐偏移，已编辑的判定盒、锚点和步骤时长会保留。",
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
            EditorGUILayout.LabelField("数据", $"{DataRoot}/{character}/frameLib.asset, <动作>.asset");

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
            string dataDir = $"{DataRoot}/{character}";
            Directory.CreateDirectory(spriteDir);
            Directory.CreateDirectory(dataDir);

            // 1. 复制图片并按项目约定导入
            var spritePaths = new Dictionary<string, string>();
            PendingTextures.Clear();
            foreach ((string dir, AnimationJson anim) in anims)
            {
                string folder = $"{spriteDir}/{Path.GetFileName(dir)}";
                Directory.CreateDirectory(folder);
                foreach (FrameJson f in anim.frames)
                {
                    string src = Path.Combine(dir, f.file);
                    if (!File.Exists(src))
                    {
                        Debug.LogError($"[SequenceFrameImporter] 缺少图片: {src}");
                        continue;
                    }
                    string dst = $"{folder}/{f.file}";
                    File.Copy(src, dst, true);
                    spritePaths[f.name] = dst;
                    PendingTextures[dst] = anim.pixelsPerUnit > 0 ? anim.pixelsPerUnit : GameConfig.PIXELS_PER_UNIT;
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

            // 2. 帧库：按帧名匹配已有帧
            string libPath = $"{dataDir}/frameLib.asset";
            var lib = AssetDatabase.LoadAssetAtPath<CharacterSpriteLibrary>(libPath);
            if (lib == null)
            {
                lib = CreateInstance<CharacterSpriteLibrary>();
                AssetDatabase.CreateAsset(lib, libPath);
            }
            var frameIndex = new Dictionary<string, int>();
            for (int i = 0; i < lib.AllFrames.Count; i++)
            {
                string frameName = lib.AllFrames[i].Name;
                if (frameName != null && !frameIndex.ContainsKey(frameName)) frameIndex[frameName] = i;
            }

            int added = 0, updated = 0;
            foreach (var (_, anim) in anims)
            {
                foreach (FrameJson f in anim.frames)
                {
                    if (!spritePaths.TryGetValue(f.name, out string path)) continue;
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        Debug.LogError($"[SequenceFrameImporter] {path} 没有导入为 Sprite");
                        continue;
                    }

                    // Sprite 为中心 pivot，渲染位置 = 脚底 + Offset (y 向上)
                    var offset = new Vector2(f.width * 0.5f - f.pivotX, f.pivotY - f.height * 0.5f);
                    if (frameIndex.TryGetValue(f.name, out int idx))
                    {
                        SpriteFrameData frame = lib.AllFrames[idx];
                        frame.Sprite = sprite;
                        frame.Offset = offset;
                        updated++;
                    }
                    else
                    {
                        frameIndex[f.name] = lib.AllFrames.Count;
                        lib.AllFrames.Add(new SpriteFrameData
                        {
                            Name = f.name,
                            Sprite = sprite,
                            Offset = offset,
                            HurtBoxes = new List<LogicBox>(),
                            HitBoxes = new List<LogicBox>(),
                        });
                        added++;
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
                        Duration = oldDurations.TryGetValue(f.name, out int d) ? d : 1,
                    })
                    .ToList();
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SequenceFrameImporter] {character}: {anims.Count} 个动作，新增 {added} 帧，更新 {updated} 帧 -> {dataDir}");
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
