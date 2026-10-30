using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KiHan.Config;
using KiHan.View.Effects;
using KiHan.View.Render;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KiHan.Editor
{
    /// <summary>
    /// 导入 Reverse/tools/export_effects.py 导出的原版特效 (effects/&lt;特效名&gt;/effect.json + textures/ + meshes/) 到角色包：
    /// - 贴图 → {角色}/Effect/Textures/，网格 → Effect/Meshes/ (外部网格用 1×1 面片 FxPlane 代替)，材质 → Effect/Materials/ (KiHan/Effect/Particle)
    /// - 每个特效生成 {角色}/Effect/{特效名}.prefab，包内资源 key 为 Effect/{特效名}：节点层级与变换照搬，
    ///   FXMaker 组件换成工程的 Fx* 组件，粒子系统按序列化路径逐项写入，根节点挂 EffectView (回收时间 = 原版特效时长)
    /// 外部工具可写入 Temp/EffectImport.request：每行一项 "导出目录|角色目录名"；脚本加载后执行一次
    /// </summary>
    public class EffectPrefabImporter : EditorWindow
    {
        [Serializable]
        private class EffectJson
        {
            public string name;
            public float lifeTime;
            public NodeJson[] nodes;
            public MaterialJson[] materials;
        }

        [Serializable]
        private class NodeJson
        {
            public string name;
            public int parent;
            public bool active;
            public float[] position;
            public float[] rotation;
            public float[] scale;
            public string mesh;
            public string renderer;
            public string[] materials;
            public int sortingOrder;
            public ComponentJson[] components;
            public EntryJson[] particleSystem;
            public EntryJson[] particleRenderer;
        }

        [Serializable]
        private class MaterialJson
        {
            public string key;
            public string texture;
            public float[] tint;
            public string blend;
            public float[] mainTexST;
        }

        [Serializable]
        private class ComponentJson
        {
            public string type;
            public float delay;
            public float duration;
            public bool loop;
            public int loopCount;
            public bool autoDestruct;
            public CurveJson[] curves;
            public float[] scrollSpeed;
            public float[] tiling;
            public float[] offset;
            public bool repeat;
            public int[] tiles;
            public int startFrame;
            public int frameCount;
            public float fps;
            public int loopingCount;
            public int rotationMode;
            public int rotationAxis;
            public float rotationValue;
            public ColorStepJson[] colors;
        }

        [Serializable]
        private class CurveJson
        {
            public string type;
            public bool[] axes;
            public float valueScale;
            public float[] from;
            public float[] to;
            public KeyJson[] keys;
        }

        [Serializable]
        private class KeyJson
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
        }

        [Serializable]
        private class ColorStepJson
        {
            public float[] color;
            public float duration;
        }

        /// <summary>
        /// 序列化属性条目：t = f 浮点 / i 整数与布尔 / c 颜色 / k AnimationCurve / g Gradient (见 export_effects.py flatten)
        /// </summary>
        [Serializable]
        private class EntryJson
        {
            public string p;
            public string t;
            public float f;
            public int i;
            public float[] a;
            public float[] b;
        }

        private const string CharacterRoot = "Assets/AssetPackages/Characters";
        private const string ShaderName = "KiHan/Effect/Particle";
        private const string RequestPath = "Temp/EffectImport.request";
        private const string PlaneMesh = "plane";

        private string _exportDir = "";
        private string _character = "";

        [MenuItem("KiHan/资源/导入原版特效 (export_effects)")]
        private static void Open()
        {
            GetWindow<EffectPrefabImporter>("导入原版特效");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("选择 export_effects.py 的输出目录 (含 textures/、meshes/ 与各特效子目录)。重复导入会覆盖同名 prefab、材质与网格。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                _exportDir = EditorGUILayout.TextField("导出目录", _exportDir);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string picked = EditorUtility.OpenFolderPanel("选择特效导出目录", _exportDir, "");
                    if (!string.IsNullOrEmpty(picked)) _exportDir = picked;
                }
            }
            _character = EditorGUILayout.TextField("角色目录名", _character);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_exportDir) || string.IsNullOrWhiteSpace(_character)))
            {
                if (GUILayout.Button("导入", GUILayout.Height(28))) Import(_exportDir, _character.Trim());
            }
        }

        [InitializeOnLoadMethod]
        private static void RunPendingRequest()
        {
            if (!File.Exists(RequestPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestPath)) return;
                string[] lines = File.ReadAllLines(RequestPath);
                File.Delete(RequestPath);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 2)
                    {
                        if (line.Trim().Length > 0) Debug.LogError($"[EffectPrefabImporter] 请求格式应为 导出目录|角色目录名: {line}");
                        continue;
                    }
                    Import(parts[0].Trim(), parts[1].Trim());
                }
            };
        }

        public static bool Import(string exportDir, string character)
        {
            string[] effectDirs = Directory.Exists(exportDir)
                ? Directory.GetDirectories(exportDir).Where(d => File.Exists(Path.Combine(d, "effect.json"))).OrderBy(d => d, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            if (effectDirs.Length == 0)
            {
                Debug.LogError($"[EffectPrefabImporter] {exportDir} 下没有特效 (effect.json)");
                return false;
            }

            string root = $"{CharacterRoot}/{character}/Effect";
            string texDir = $"{root}/Textures", matDir = $"{root}/Materials", meshDir = $"{root}/Meshes";
            foreach (string dir in new[] { texDir, matDir, meshDir }) Directory.CreateDirectory(dir);

            ImportTextures(Path.Combine(exportDir, "textures"), texDir);
            var meshes = new Dictionary<string, Mesh>();
            var materials = new Dictionary<string, Material>();
            Shader shader = Shader.Find(ShaderName);
            int unknownProps = 0;

            foreach (string dir in effectDirs)
            {
                var effect = JsonUtility.FromJson<EffectJson>(File.ReadAllText(Path.Combine(dir, "effect.json")));
                foreach (MaterialJson m in effect.materials ?? Array.Empty<MaterialJson>())
                {
                    if (!materials.ContainsKey(m.key)) materials[m.key] = SaveMaterial(m, matDir, texDir, shader);
                }

                var nodes = new GameObject[effect.nodes.Length];
                for (int n = 0; n < effect.nodes.Length; n++)
                {
                    NodeJson node = effect.nodes[n];
                    var go = new GameObject(node.name);
                    nodes[n] = go;
                    if (node.parent >= 0) go.transform.SetParent(nodes[node.parent].transform, false);
                    go.transform.localPosition = ToVector3(node.position);
                    go.transform.localRotation = new Quaternion(node.rotation[0], node.rotation[1], node.rotation[2], node.rotation[3]);
                    go.transform.localScale = ToVector3(node.scale);
                    unknownProps += BuildNode(go, node, exportDir, meshDir, meshes, materials);
                }

                GameObject rootGo = nodes[0];
                EffectView view = rootGo.AddComponent<EffectView>();
                var so = new SerializedObject(view);
                so.FindProperty("fallbackDuration").floatValue = effect.lifeTime > 0f ? effect.lifeTime : EstimateLifetime(effect);
                so.ApplyModifiedPropertiesWithoutUndo();

                for (int n = 0; n < effect.nodes.Length; n++)
                {
                    if (!effect.nodes[n].active) nodes[n].SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(rootGo, $"{root}/{effect.name}.prefab");
                Object.DestroyImmediate(rootGo);
            }

            AssetDatabase.SaveAssets();
            // 刷新包内清单，使新 prefab 进入包内资源表 (key = Effect/{特效名})
            string manifestPath = CharacterPackages.FindManifestPath($"{root}/x");
            var manifest = manifestPath != null ? AssetDatabase.LoadAssetAtPath<CharacterManifest>(manifestPath) : null;
            if (manifest != null) CharacterPackages.Refresh(manifest);

            Debug.Log($"[EffectPrefabImporter] {character}: {effectDirs.Length} 个特效 -> {root}；材质 {materials.Count}，网格 {meshes.Count}，未识别的粒子属性 {unknownProps} 项");
            return true;
        }

        private static int BuildNode(GameObject go, NodeJson node, string exportDir, string meshDir,
            Dictionary<string, Mesh> meshes, Dictionary<string, Material> materials)
        {
            int unknown = 0;
            // 贴图缺失的材质 (原版贴图在未收集的外部包中) 不使用：工程 Shader 无贴图时按纯白采样，会画出大片白色方块
            Material[] mats = (node.materials ?? Array.Empty<string>())
                .Select(k => k != null && materials.TryGetValue(k, out Material m) ? m : null)
                .Where(m => m != null && m.mainTexture != null).ToArray();

            if (node.renderer == "mesh" && !string.IsNullOrEmpty(node.mesh))
            {
                go.AddComponent<MeshFilter>().sharedMesh = LoadMesh(node.mesh, exportDir, meshDir, meshes);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                mr.sortingOrder = node.sortingOrder;
                if (mats.Length == 0) mr.enabled = false;
                else go.AddComponent<FxRendererState>();
            }
            else if (node.particleSystem != null && node.particleSystem.Length > 0)
            {
                var ps = go.AddComponent<ParticleSystem>();
                unknown += ApplyEntries(new SerializedObject(ps), node.particleSystem);
                var psr = go.GetComponent<ParticleSystemRenderer>();
                if (node.particleRenderer != null) unknown += ApplyEntries(new SerializedObject(psr), node.particleRenderer);
                psr.sharedMaterials = mats;
                // 材质全部缺失 (原版引用外部包材质) 时关闭渲染器：Unity 2022.3 渲染 0 材质的粒子渲染器会原生崩溃 (Camera.Render 内 SIGSEGV)，
                // 粒子仍正常模拟 (子发射器等依赖不受影响)
                if (mats.Length == 0) psr.enabled = false;
                psr.sortingOrder = node.sortingOrder;
                if (psr.enabled) go.AddComponent<FxRendererState>();
            }

            foreach (ComponentJson c in node.components ?? Array.Empty<ComponentJson>())
            {
                switch (c.type)
                {
                    case "Curve":
                        var curve = go.AddComponent<FxCurveAnimation>();
                        curve.delay = c.delay;
                        curve.duration = c.duration;
                        curve.loop = c.loop;
                        curve.loopCount = c.loopCount;
                        curve.autoDestruct = c.autoDestruct;
                        curve.curves = (c.curves ?? Array.Empty<CurveJson>()).Select(ToFxCurve).ToArray();
                        break;
                    case "Uv":
                        var uv = go.AddComponent<FxUvAnimation>();
                        uv.scrollSpeed = ToVector2(c.scrollSpeed);
                        uv.tiling = ToVector2(c.tiling);
                        uv.offset = ToVector2(c.offset);
                        uv.repeat = c.repeat;
                        break;
                    case "Sprite":
                        var sprite = go.AddComponent<FxSpriteAnimation>();
                        sprite.delay = c.delay;
                        sprite.tiles = new Vector2Int(c.tiles[0], c.tiles[1]);
                        sprite.startFrame = c.startFrame;
                        sprite.frameCount = c.frameCount;
                        sprite.fps = c.fps;
                        sprite.loop = c.loop;
                        sprite.loopingCount = c.loopingCount;
                        sprite.autoDestruct = c.autoDestruct;
                        break;
                    case "Billboard":
                        var billboard = go.AddComponent<FxBillboard>();
                        billboard.rotationMode = c.rotationMode;
                        billboard.rotationAxis = c.rotationAxis;
                        billboard.rotationValue = c.rotationValue;
                        break;
                    case "ChangeColor":
                        var change = go.AddComponent<FxChangeColor>();
                        change.delay = c.delay;
                        change.steps = (c.colors ?? Array.Empty<ColorStepJson>())
                            .Select(s => new FxColorStep { color = ToColor(s.color), duration = s.duration }).ToArray();
                        break;
                    case "DontActive":
                        node.active = false;
                        break;
                }
            }
            return unknown;
        }

        // ------------------------------------------------------------ 资源

        private static void ImportTextures(string srcDir, string texDir)
        {
            if (!Directory.Exists(srcDir)) return;
            var copied = new List<string>();
            foreach (string src in Directory.GetFiles(srcDir, "*.png"))
            {
                string dst = $"{texDir}/{Path.GetFileName(src)}";
                File.Copy(src, dst, true);
                copied.Add(dst);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in copied)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (importer.textureType == TextureImporterType.Default && importer.wrapMode == TextureWrapMode.Repeat
                    && !importer.mipmapEnabled && importer.alphaIsTransparency) continue;
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static Material SaveMaterial(MaterialJson m, string matDir, string texDir, Shader shader)
        {
            string path = $"{matDir}/{SafeName(m.key)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (!string.IsNullOrEmpty(m.texture))
            {
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{Path.GetFileName(m.texture)}");
            }
            mat.mainTextureScale = new Vector2(m.mainTexST[0], m.mainTexST[1]);
            mat.mainTextureOffset = new Vector2(m.mainTexST[2], m.mainTexST[3]);
            mat.SetColor("_TintColor", ToColor(m.tint));
            bool additive = m.blend == "Additive";
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh LoadMesh(string key, string exportDir, string meshDir, Dictionary<string, Mesh> meshes)
        {
            if (meshes.TryGetValue(key, out Mesh cached)) return cached;
            Mesh mesh;
            if (key == PlaneMesh)
            {
                mesh = SaveMesh($"{meshDir}/FxPlane.asset", new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                }, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) }, null, new[] { 0, 2, 1, 2, 3, 1 });
            }
            else
            {
                MeshFile data = MeshFile.Parse(File.ReadAllText(Path.Combine(exportDir, key)));
                mesh = SaveMesh($"{meshDir}/{SafeName(Path.GetFileNameWithoutExtension(key))}.asset", data.Vertices, data.Uv, data.Colors, data.Triangles);
            }
            meshes[key] = mesh;
            return mesh;
        }

        private static Mesh SaveMesh(string path, Vector3[] vertices, Vector2[] uv, Color[] colors, int[] triangles)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.vertices = vertices;
            if (uv != null && uv.Length == vertices.Length) mesh.uv = uv;
            mesh.colors = colors != null && colors.Length == vertices.Length ? colors : Enumerable.Repeat(Color.white, vertices.Length).ToArray();
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// 网格 JSON：vertices / uv / colors 为嵌套数组，JsonUtility 不支持，按数字序列手动解析
        /// </summary>
        private class MeshFile
        {
            public Vector3[] Vertices;
            public Vector2[] Uv;
            public Color[] Colors;
            public int[] Triangles;

            public static MeshFile Parse(string json)
            {
                return new MeshFile
                {
                    Vertices = Group(Numbers(json, "vertices"), 3).Select(v => new Vector3(v[0], v[1], v[2])).ToArray(),
                    Uv = Group(Numbers(json, "uv"), 2).Select(v => new Vector2(v[0], v[1])).ToArray(),
                    Colors = Group(Numbers(json, "colors"), 4).Select(v => new Color(v[0], v[1], v[2], v[3])).ToArray(),
                    Triangles = Numbers(json, "triangles").Select(v => (int)v).ToArray(),
                };
            }

            private static List<float> Numbers(string json, string key)
            {
                var result = new List<float>();
                int start = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
                if (start < 0) return result;
                start = json.IndexOf('[', start);
                int depth = 0, i = start;
                var token = new System.Text.StringBuilder();
                for (; i < json.Length; i++)
                {
                    char ch = json[i];
                    if (ch == '[') depth++;
                    else if (ch == ']') depth--;
                    if (char.IsDigit(ch) || ch == '-' || ch == '.' || ch == 'e' || ch == 'E' || ch == '+')
                    {
                        token.Append(ch);
                        continue;
                    }
                    if (token.Length > 0)
                    {
                        result.Add(float.Parse(token.ToString(), System.Globalization.CultureInfo.InvariantCulture));
                        token.Clear();
                    }
                    if (depth == 0) break;
                }
                return result;
            }

            private static IEnumerable<float[]> Group(List<float> values, int size)
            {
                for (int i = 0; i + size <= values.Count; i += size) yield return values.GetRange(i, size).ToArray();
            }
        }

        // ------------------------------------------------------------ 序列化属性写入

        private static int ApplyEntries(SerializedObject so, EntryJson[] entries)
        {
            int unknown = 0;
            foreach (EntryJson e in entries)
            {
                if (!ApplyEntry(so, e)) unknown++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return unknown;
        }

        private static bool ApplyEntry(SerializedObject so, EntryJson e)
        {
            SerializedProperty prop = so.FindProperty(e.p);
            if (prop == null) return ApplyComponent(so, e);
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    prop.intValue = e.t == "f" ? (int)e.f : e.i;
                    return true;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = e.i != 0;
                    return true;
                case SerializedPropertyType.Float:
                    prop.floatValue = e.t == "f" ? e.f : e.i;
                    return true;
                case SerializedPropertyType.Color:
                    if (e.t != "c") return false;
                    prop.colorValue = ToColor(e.a);
                    return true;
                case SerializedPropertyType.AnimationCurve:
                    if (e.t != "k") return false;
                    prop.animationCurveValue = ToCurve(e.a, e.i);
                    return true;
                case SerializedPropertyType.Gradient:
                    if (e.t != "g") return false;
                    prop.gradientValue = ToGradient(e);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// "a.b.x" 找不到时，按父属性 (Vector2/3/4、Quaternion、Color) 写入分量
        /// </summary>
        private static bool ApplyComponent(SerializedObject so, EntryJson e)
        {
            int dot = e.p.LastIndexOf('.');
            if (dot < 0 || e.t != "f") return false;
            SerializedProperty parent = so.FindProperty(e.p.Substring(0, dot));
            if (parent == null) return false;
            string axis = e.p.Substring(dot + 1);
            int index = axis == "x" ? 0 : axis == "y" ? 1 : axis == "z" ? 2 : axis == "w" ? 3 : -1;
            if (index < 0) return false;
            switch (parent.propertyType)
            {
                case SerializedPropertyType.Vector2:
                    Vector2 v2 = parent.vector2Value;
                    v2[index] = e.f;
                    parent.vector2Value = v2;
                    return true;
                case SerializedPropertyType.Vector3:
                    Vector3 v3 = parent.vector3Value;
                    v3[index] = e.f;
                    parent.vector3Value = v3;
                    return true;
                case SerializedPropertyType.Vector4:
                    Vector4 v4 = parent.vector4Value;
                    v4[index] = e.f;
                    parent.vector4Value = v4;
                    return true;
                case SerializedPropertyType.Quaternion:
                    Quaternion q = parent.quaternionValue;
                    q[index] = e.f;
                    parent.quaternionValue = q;
                    return true;
                default:
                    return false;
            }
        }

        private static AnimationCurve ToCurve(float[] flat, int wrap)
        {
            var keys = new Keyframe[flat.Length / 4];
            for (int k = 0; k < keys.Length; k++) keys[k] = new Keyframe(flat[k * 4], flat[k * 4 + 1], flat[k * 4 + 2], flat[k * 4 + 3]);
            return new AnimationCurve(keys) { preWrapMode = (WrapMode)(wrap >> 4), postWrapMode = (WrapMode)(wrap & 15) };
        }

        private static Gradient ToGradient(EntryJson e)
        {
            var colors = new GradientColorKey[e.a.Length / 4];
            for (int k = 0; k < colors.Length; k++) colors[k] = new GradientColorKey(new Color(e.a[k * 4], e.a[k * 4 + 1], e.a[k * 4 + 2]), e.a[k * 4 + 3]);
            var alphas = new GradientAlphaKey[e.b.Length / 2];
            for (int k = 0; k < alphas.Length; k++) alphas[k] = new GradientAlphaKey(e.b[k * 2], e.b[k * 2 + 1]);
            var gradient = new Gradient { mode = (GradientMode)e.i };
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static FxCurve ToFxCurve(CurveJson c)
        {
            var keys = (c.keys ?? Array.Empty<KeyJson>()).Select(k => new Keyframe(k.time, k.value, k.inTangent, k.outTangent)).ToArray();
            return new FxCurve
            {
                type = (FxCurveType)Enum.Parse(typeof(FxCurveType), c.type),
                x = c.axes[0], y = c.axes[1], z = c.axes[2], w = c.axes[3],
                valueScale = c.valueScale,
                from = ToColor(c.from),
                to = ToColor(c.to),
                curve = new AnimationCurve(keys),
            };
        }

        private static float EstimateLifetime(EffectJson effect)
        {
            float life = 0.5f;
            foreach (NodeJson node in effect.nodes)
            {
                foreach (ComponentJson c in node.components ?? Array.Empty<ComponentJson>())
                {
                    if (c.type == "Curve") life = Mathf.Max(life, c.delay + c.duration * Mathf.Max(1, c.loop ? c.loopCount : 1));
                    if (c.type == "Sprite" && c.fps > 0f) life = Mathf.Max(life, c.delay + c.frameCount / c.fps);
                }
            }
            return Mathf.Min(life, 5f);
        }

        private static Vector3 ToVector3(float[] v) => new Vector3(v[0], v[1], v[2]);
        private static Vector2 ToVector2(float[] v) => v != null && v.Length >= 2 ? new Vector2(v[0], v[1]) : Vector2.zero;
        private static Color ToColor(float[] c) => c != null && c.Length >= 4 ? new Color(c[0], c[1], c[2], c[3]) : Color.white;

        private static string SafeName(string name)
        {
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return name;
        }
    }
}
