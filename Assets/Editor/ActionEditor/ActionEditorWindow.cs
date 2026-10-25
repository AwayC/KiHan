using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KiHan.Config;
using KiHan.Logic;
using UnityEditor;
using UnityEngine;

namespace KiHan.Editor
{
    /// <summary>
    /// 动作编辑器：编辑角色包 (Character.asset 所在目录) 下的 Config/actions.json
    ///
    /// 布局：左侧动作列表 | 中间预览 + 时间轴 (判定阶段 / 霸体 / 取消窗口 / 移动 / 位移 / 事件) | 右侧属性面板
    /// - 时间轴条目可拖动整体平移、拖动两端改区间，右键空白处在该帧新增，右键条目删除
    /// - 所有修改可撤销 (Ctrl+Z)，Ctrl+S 保存
    /// - 保存时只改动编辑过的字段，未修改的动作按原文输出 (见 ActionJson)
    /// </summary>
    public partial class ActionEditorWindow : EditorWindow
    {
        private const string ActionsFileName = "actions.json";
        private const float ListWidth = 210f;
        private const float InspectorWidth = 380f;

        [SerializeField] private ActionEditorState _state;
        [SerializeField] private string _characterDir;
        [SerializeField] private int _selected = -1;
        [SerializeField] private int _frame;
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private bool _dirty;
        [SerializeField] private Vector2 _listScroll;
        [SerializeField] private Vector2 _inspectorScroll;

        private SerializedObject _so;
        private readonly Dictionary<string, AnimationFrameData> _anims = new Dictionary<string, AnimationFrameData>();
        private string[] _animNames = Array.Empty<string>();

        [MenuItem("KiHan/动作编辑器")]
        public static void Open()
        {
            var window = GetWindow<ActionEditorWindow>();
            window.titleContent = new GUIContent("动作编辑器");
            window.minSize = new Vector2(1100, 600);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("动作编辑器");
            wantsMouseMove = true;
            saveChangesMessage = "动作配置有未保存的修改，是否保存？";
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;

            if (_state != null && !string.IsNullOrEmpty(_state.jsonPath))
            {
                // 域重载后 (_state 为 DontSave 对象，跨重载保留) 只需重建缓存
                LoadAnimations();
            }
            else if (string.IsNullOrEmpty(_characterDir))
            {
                string[] dirs = CharacterDirs();
                if (dirs.Length > 0) LoadCharacter(dirs[0]);
            }
            else
            {
                LoadCharacter(_characterDir);
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                Undo.ClearUndo(_state);
                DestroyImmediate(_state);
            }
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        private void OnUndoRedo()
        {
            if (_state == null) return;
            MarkDirty();
            ClampSelection();
            Repaint();
        }

        private void MarkDirty()
        {
            _dirty = true;
            hasUnsavedChanges = true;
            _validation = null;
        }

        #region 读写

        /// <summary>
        /// 全部角色包的根目录 (清单所在目录)
        /// </summary>
        private static string[] CharacterDirs()
        {
            List<CharacterManifest> manifests = CharacterPackages.FindManifests();
            var dirs = new string[manifests.Count];
            for (int i = 0; i < dirs.Length; i++) dirs[i] = CharacterPackages.PackageRoot(manifests[i]);
            return dirs;
        }

        private static string ActionsPath(string dir) => $"{dir}/{CharacterPackages.ConfigFolder}/{ActionsFileName}";

        private bool ConfirmDiscard()
        {
            if (!_dirty) return true;
            int choice = EditorUtility.DisplayDialogComplex("动作编辑器", "当前动作配置有未保存的修改。", "保存", "取消", "放弃修改");
            if (choice == 1) return false;
            if (choice == 0) Save();
            return true;
        }

        private void LoadCharacter(string dir)
        {
            _characterDir = dir;
            if (_state == null)
            {
                _state = CreateInstance<ActionEditorState>();
                _state.hideFlags = HideFlags.DontSave;
            }
            else
            {
                Undo.ClearUndo(_state);
            }

            _state.entries.Clear();
            _state.jsonPath = string.Empty;
            _state.rootSource = string.Empty;
            _so = null;
            _selected = -1;
            _selection = default;
            _frame = 0;
            _dirty = false;
            hasUnsavedChanges = false;
            _validation = null;

            LoadAnimations();

            string path = ActionsPath(dir);
            if (!File.Exists(path)) return;

            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            JNode root;
            ActionDataContainer container;
            try
            {
                root = ActionJson.Parse(text);
                container = JsonUtility.FromJson<ActionDataContainer>(text);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("动作编辑器", $"{path} 解析失败：\n{e.Message}", "确定");
                return;
            }

            JNode data = root.Get("data");
            ActionData[] actions = container?.data ?? Array.Empty<ActionData>();
            _state.jsonPath = path;
            _state.rootSource = text;
            for (int i = 0; i < actions.Length; i++)
            {
                _state.entries.Add(new ActionEditorState.Entry
                {
                    action = actions[i],
                    source = data != null && i < data.Items.Count ? data.Items[i].Raw : null
                });
            }
            _selected = _state.entries.Count > 0 ? 0 : -1;
        }

        private void LoadAnimations()
        {
            _anims.Clear();
            if (string.IsNullOrEmpty(_characterDir) || !AssetDatabase.IsValidFolder(_characterDir))
            {
                _animNames = Array.Empty<string>();
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationFrameData", new[] { _characterDir }))
            {
                var anim = AssetDatabase.LoadAssetAtPath<AnimationFrameData>(AssetDatabase.GUIDToAssetPath(guid));
                if (anim != null && !_anims.ContainsKey(anim.name)) _anims.Add(anim.name, anim);
            }
            _animNames = new string[_anims.Count];
            _anims.Keys.CopyTo(_animNames, 0);
            Array.Sort(_animNames, StringComparer.OrdinalIgnoreCase);
        }

        private void CreateActionsFile()
        {
            string path = ActionsPath(_characterDir);
            string configDir = $"{_characterDir}/{CharacterPackages.ConfigFolder}";
            if (!AssetDatabase.IsValidFolder(configDir)) AssetDatabase.CreateFolder(_characterDir, CharacterPackages.ConfigFolder);
            string idleAnim = _anims.ContainsKey("Idle") ? "Idle" : (_animNames.Length > 0 ? _animNames[0] : "Idle");
            var sb = new StringBuilder();
            sb.Append("{\n  \"data\": [\n    {\n");
            sb.Append("      \"id\": \"idle\",\n");
            sb.Append($"      \"animKey\": \"{idleAnim}\",\n");
            sb.Append("      \"catalog\": \"Base\",\n");
            sb.Append("      \"keepPlayingAnim\": true,\n");
            sb.Append("      \"autoNextActionId\": \"idle\"\n");
            sb.Append("    }\n  ]\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            var manifest = AssetDatabase.LoadAssetAtPath<CharacterManifest>($"{_characterDir}/{CharacterManifest.FileName}.asset");
            if (manifest != null) CharacterPackages.Refresh(manifest);
            LoadCharacter(_characterDir);
        }

        private void Save()
        {
            if (_state == null || string.IsNullOrEmpty(_state.jsonPath)) return;

            List<Issue> issues = Validate();
            int errors = 0;
            foreach (Issue issue in issues)
            {
                if (issue.Error) errors++;
            }
            if (errors > 0 && !EditorUtility.DisplayDialog("动作编辑器", $"校验发现 {errors} 个错误 (见时间轴下方校验结果)，仍要保存吗？", "仍然保存", "取消"))
            {
                _validation = issues;
                return;
            }

            JNode root = ActionJson.Parse(_state.rootSource).CloneShallow();
            JNode data = JNode.Arr();
            foreach (ActionEditorState.Entry entry in _state.entries)
            {
                JNode original = string.IsNullOrEmpty(entry.source) ? null : ActionJson.Parse(entry.source);
                data.Items.Add(ActionJson.Merge(original, ActionJson.ToNode(entry.action, typeof(ActionData))));
            }
            root.Set("data", data);

            string text = ActionJson.Write(root);
            File.WriteAllText(_state.jsonPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_state.jsonPath);

            // 以新文件为原文，后续保存继续保持最小改动
            JNode saved = ActionJson.Parse(text);
            JNode savedData = saved.Get("data");
            for (int i = 0; i < _state.entries.Count; i++)
            {
                _state.entries[i].source = savedData.Items[i].Raw;
            }
            _state.rootSource = text;
            _dirty = false;
            hasUnsavedChanges = false;
            _validation = issues;
            ShowNotification(new GUIContent($"已保存 {Path.GetFileName(_state.jsonPath)}"));
        }

        #endregion

        #region 主界面

        private ActionData Current => _state != null && _selected >= 0 && _selected < _state.entries.Count ? _state.entries[_selected].action : null;

        private string ActionPath(int index) => $"entries.Array.data[{index}].action";

        private void ClampSelection()
        {
            if (_state == null) return;
            if (_selected >= _state.entries.Count) _selected = _state.entries.Count - 1;
            if (!SelectionValid()) _selection = default;
        }

        private void OnGUI()
        {
            HandleShortcuts();
            DrawToolbar();

            if (_state == null) return;
            if (_so == null || _so.targetObject != _state) _so = new SerializedObject(_state);
            _so.Update();

            if (string.IsNullOrEmpty(_state.jsonPath))
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox($"{_characterDir} 下没有 {CharacterPackages.ConfigFolder}/{ActionsFileName}。", MessageType.Info);
                if (!string.IsNullOrEmpty(_characterDir) && GUILayout.Button("创建 actions.json (含 idle 动作)", GUILayout.Height(28), GUILayout.Width(260)))
                {
                    CreateActionsFile();
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawActionList();
                // 固定中栏宽度：中栏内容再长也不会把右侧属性面板挤出窗口
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(CenterWidth)))
                {
                    DrawCenter();
                }
                DrawInspector();
            }

            if (_so.ApplyModifiedProperties()) MarkDirty();
        }

        private void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if ((e.control || e.command) && e.keyCode == KeyCode.S)
            {
                Save();
                e.Use();
                return;
            }

            // 文本输入中不抢方向键 / 空格 / Delete
            if (EditorGUIUtility.editingTextField || GUIUtility.keyboardControl != 0) return;

            int total = TotalFrames(Current);
            switch (e.keyCode)
            {
                case KeyCode.LeftArrow:
                    _frame = Mathf.Max(0, _frame - 1);
                    _playing = false;
                    e.Use();
                    break;
                case KeyCode.RightArrow:
                    _frame = Mathf.Min(total - 1, _frame + 1);
                    _playing = false;
                    e.Use();
                    break;
                case KeyCode.Space:
                    TogglePlay();
                    e.Use();
                    break;
                case KeyCode.Delete:
                    if (SelectionValid())
                    {
                        DeleteSelectedItem();
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string[] dirs = CharacterDirs();
                string[] labels = new string[dirs.Length];
                int current = -1;
                for (int i = 0; i < dirs.Length; i++)
                {
                    string name = Path.GetFileName(dirs[i]);
                    labels[i] = File.Exists(ActionsPath(dirs[i])) ? name : $"{name} (无 actions.json)";
                    if (dirs[i] == _characterDir) current = i;
                }

                GUILayout.Label("角色", GUILayout.Width(30));
                int picked = EditorGUILayout.Popup(current, labels, EditorStyles.toolbarPopup, GUILayout.Width(180));
                if (picked != current && picked >= 0 && ConfirmDiscard()) LoadCharacter(dirs[picked]);

                if (GUILayout.Button("重新加载", EditorStyles.toolbarButton, GUILayout.Width(64)) && ConfirmDiscard())
                {
                    LoadCharacter(_characterDir);
                }

                GUI.enabled = _state != null && !string.IsNullOrEmpty(_state.jsonPath);
                var saveStyle = new GUIStyle(EditorStyles.toolbarButton) { fontStyle = _dirty ? FontStyle.Bold : FontStyle.Normal };
                if (GUILayout.Button(_dirty ? "保存 *" : "保存", saveStyle, GUILayout.Width(56))) Save();
                if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(44))) _validation = Validate();
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
                if (_state != null && !string.IsNullOrEmpty(_state.jsonPath))
                {
                    GUILayout.Label(_dirty ? $"{_state.jsonPath} (未保存)" : _state.jsonPath, EditorStyles.miniLabel);
                    if (GUILayout.Button("定位文件", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(_state.jsonPath));
                    }
                }
            }
        }

        private void DrawActionList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
                var idStyle = new GUIStyle(EditorStyles.label) { richText = true };
                for (int i = 0; i < _state.entries.Count; i++)
                {
                    ActionData a = _state.entries[i].action;
                    if (!MatchesSearch(a)) continue;

                    Rect row = GUILayoutUtility.GetRect(ListWidth - 16, 20, GUILayout.ExpandWidth(true));
                    if (i == _selected) EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.9f, 0.45f));
                    else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.06f));

                    bool missingAnim = !string.IsNullOrEmpty(a.animKey) && !_anims.ContainsKey(a.animKey);
                    string catalog = string.IsNullOrEmpty(a.catalog) ? string.Empty : $"  <color=#888888>{a.catalog}</color>";
                    string warn = missingAnim ? "  <color=#ff6060>缺动画</color>" : string.Empty;
                    GUI.Label(new Rect(row.x + 4, row.y + 2, row.width - 4, row.height), $"{a.id}{catalog}{warn}", idStyle);

                    // 透明按钮：与内置控件一样只响应滚动视图可见区域内的点击
                    if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SelectAction(i);
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新建")) AddAction(null);
                    GUI.enabled = Current != null;
                    if (GUILayout.Button("复制")) AddAction(Current);
                    if (GUILayout.Button("删除")) DeleteAction();
                    if (GUILayout.Button("↑", GUILayout.Width(24))) MoveAction(-1);
                    if (GUILayout.Button("↓", GUILayout.Width(24))) MoveAction(1);
                    GUI.enabled = true;
                }
            }
        }

        private bool MatchesSearch(ActionData a)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            return Contains(a.id, _search) || Contains(a.animKey, _search) || Contains(a.catalog, _search);
        }

        private static bool Contains(string s, string part) => !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private void SelectAction(int index)
        {
            if (index == _selected) return;
            _selected = index;
            _selection = default;
            _frame = 0;
            _playing = false;
            GUI.FocusControl(null);
        }

        private void AddAction(ActionData template)
        {
            Undo.RecordObject(_state, template != null ? "复制动作" : "新建动作");
            ActionData action;
            string source = null;
            if (template != null)
            {
                // 经 JsonUtility 深拷贝；保留原文写法便于复制后只改少量字段
                action = JsonUtility.FromJson<ActionData>(JsonUtility.ToJson(template));
                action.id = UniqueId(template.id + "_copy");
                source = _state.entries[_selected].source;
            }
            else
            {
                action = new ActionData
                {
                    id = UniqueId("new_action"),
                    animKey = _animNames.Length > 0 ? _animNames[0] : string.Empty,
                    autoNextActionId = CharacterFactory.DefaultActionId
                };
            }

            int insertAt = _selected >= 0 ? _selected + 1 : _state.entries.Count;
            _state.entries.Insert(insertAt, new ActionEditorState.Entry { action = action, source = source });
            MarkDirty();
            SelectAction(insertAt);
        }

        private void DeleteAction()
        {
            ActionData a = Current;
            if (a == null) return;
            if (!EditorUtility.DisplayDialog("动作编辑器", $"删除动作 {a.id}？(可撤销)", "删除", "取消")) return;
            Undo.RecordObject(_state, "删除动作");
            _state.entries.RemoveAt(_selected);
            MarkDirty();
            _selection = default;
            ClampSelection();
        }

        private void MoveAction(int delta)
        {
            int to = _selected + delta;
            if (_selected < 0 || to < 0 || to >= _state.entries.Count) return;
            Undo.RecordObject(_state, "移动动作");
            ActionEditorState.Entry entry = _state.entries[_selected];
            _state.entries.RemoveAt(_selected);
            _state.entries.Insert(to, entry);
            _selected = to;
            MarkDirty();
        }

        private string UniqueId(string baseId)
        {
            string id = baseId;
            for (int n = 2; FindAction(id) >= 0; n++) id = $"{baseId}{n}";
            return id;
        }

        private int FindAction(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _state.entries.Count; i++)
            {
                if (_state.entries[i].action.id == id) return i;
            }
            return -1;
        }

        private void DrawCenter()
        {
            ActionData a = Current;
            if (a == null)
            {
                EditorGUILayout.HelpBox("左侧选择一个动作。", MessageType.Info);
                GUILayout.FlexibleSpace();
                DrawValidation();
                return;
            }

            DrawHeader(a);
            DrawPreview(a);
            DrawTransport(a);
            DrawTimeline(a);
            GUILayout.FlexibleSpace();
            DrawValidation();
        }

        /// <summary>
        /// 动作名、动画信息与动作跳转关系 (点击跳转)
        /// </summary>
        private void DrawHeader(ActionData a)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(a.id, EditorStyles.boldLabel);
                int total = TotalFrames(a);
                string animInfo = _anims.ContainsKey(a.animKey ?? string.Empty)
                    ? $"动画 {a.animKey} · {total} 帧 · {total * GameConfig.RENDER_TICK_TIME:F2}s"
                    : $"动画 {a.animKey} 不存在 (时间轴按配置推算 {total} 帧)";
                GUILayout.Label(animInfo, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            var outgoing = new List<KeyValuePair<string, string>>();
            CollectOutgoing(a, outgoing);
            var incoming = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < _state.entries.Count; i++)
            {
                if (i == _selected) continue;
                var refs = new List<KeyValuePair<string, string>>();
                CollectOutgoing(_state.entries[i].action, refs);
                foreach (KeyValuePair<string, string> r in refs)
                {
                    if (r.Value == a.id) incoming.Add(new KeyValuePair<string, string>(r.Key, _state.entries[i].action.id));
                }
            }

            DrawChipRows("去向", outgoing);
            DrawChipRows("来源", incoming);
        }

        private float CenterWidth => Mathf.Max(320f, position.width - ListWidth - InspectorWidth - 12f);

        /// <summary>
        /// 跳转按钮按中栏宽度自动换行
        /// </summary>
        private void DrawChipRows(string title, List<KeyValuePair<string, string>> refs)
        {
            const float titleWidth = 30f;
            float maxWidth = CenterWidth - 8f;
            GUIStyle style = EditorStyles.miniButton;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title, EditorStyles.miniBoldLabel, GUILayout.Width(titleWidth));
            float used = titleWidth;
            if (refs.Count == 0) GUILayout.Label("—", EditorStyles.miniLabel);

            foreach (KeyValuePair<string, string> r in refs)
            {
                var content = new GUIContent($"{r.Key} → {r.Value}");
                float w = Mathf.Min(style.CalcSize(content).x, maxWidth - titleWidth);
                if (used + w + style.margin.horizontal > maxWidth && used > titleWidth)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(titleWidth + 4f);
                    used = titleWidth;
                }

                int index = FindAction(r.Value);
                GUI.color = index >= 0 ? Color.white : new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button(content, style, GUILayout.Width(w)) && index >= 0) SelectAction(index);
                GUI.color = Color.white;
                used += w + style.margin.horizontal;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 动作引用的其他动作 id：(引用方式, 目标 id)
        /// </summary>
        private static void CollectOutgoing(ActionData a, List<KeyValuePair<string, string>> refs)
        {
            if (!string.IsNullOrEmpty(a.autoNextActionId) && a.autoNextActionId != a.id) refs.Add(new KeyValuePair<string, string>("结束", a.autoNextActionId));
            AddChange(refs, "落地", a.onLand?.changeType ?? 0, a.onLand?.param);
            if (a.attacks != null)
            {
                for (int i = 0; i < a.attacks.Length; i++)
                {
                    AttackData atk = a.attacks[i];
                    AddChange(refs, $"命中#{i}", atk.selfActionChange?.changeType ?? 0, atk.selfActionChange?.param);
                    AddChange(refs, $"抓取释放#{i}", atk.grab.releaseActionChange?.changeType ?? 0, atk.grab.releaseActionChange?.param);
                    if (!string.IsNullOrEmpty(atk.hurtAction)) refs.Add(new KeyValuePair<string, string>($"对手受击#{i}", atk.hurtAction));
                }
            }
            if (a.defensePhase != null)
            {
                for (int i = 0; i < a.defensePhase.Length; i++)
                {
                    AddChange(refs, $"受击#{i}", (int)a.defensePhase[i].selfActionChange.changeType, a.defensePhase[i].selfActionChange.param);
                    AddChange(refs, $"对手被反#{i}", (int)a.defensePhase[i].attackerActionChange.changeType, a.defensePhase[i].attackerActionChange.param);
                }
            }
        }

        private static void AddChange(List<KeyValuePair<string, string>> refs, string how, int changeType, string param)
        {
            if (changeType == (int)ActionChangeType.ChangeToActionId && !string.IsNullOrEmpty(param)) refs.Add(new KeyValuePair<string, string>(how, param));
        }

        #endregion

        #region 属性面板

        private static readonly Dictionary<string, string> FieldLabels = new Dictionary<string, string>
        {
            { "id", "id 动作 ID" },
            { "animKey", "animKey 动画" },
            { "catalog", "catalog 分类" },
            { "cancelTag", "cancelTag 切入标签" },
            { "beCancelledTag", "beCancelledTag 可被取消窗口" },
            { "tempBeCancelledTag", "tempBeCancelledTag 临时取消窗口" },
            { "commands", "commands 触发指令" },
            { "inputAcceptance", "inputAcceptance 移动输入" },
            { "autoNextActionId", "autoNextActionId 结束后" },
            { "keepPlayingAnim", "keepPlayingAnim 循环播放" },
            { "autoTerminate", "autoTerminate 松开即结束" },
            { "inverse", "inverse 反向" },
            { "facing", "facing (未使用)" },
            { "attacks", "attacks 伤害段" },
            { "attackPhase", "attackPhase 攻击阶段" },
            { "defensePhase", "defensePhase 受击阶段" },
            { "rootMotion", "rootMotion 位移" },
            { "priority", "priority 优先级" },
            { "onLand", "onLand 落地" },
            { "armorPhase", "armorPhase 霸体" },
            { "events", "events 事件" },
            { "breakHurt", "breakHurt 受击中可用" },
        };

        private void DrawInspector()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(InspectorWidth)))
            {
                ActionData a = Current;
                if (a == null) return;

                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                EditorGUIUtility.labelWidth = 150;

                if (SelectionValid())
                {
                    DrawSelectedItemInspector(a);
                }
                else
                {
                    DrawActionInspector();
                }

                EditorGUIUtility.labelWidth = 0;
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawActionInspector()
        {
            SerializedProperty action = _so.FindProperty(ActionPath(_selected));
            if (action == null) return;

            SerializedProperty end = action.GetEndProperty();
            SerializedProperty it = action.Copy();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                enterChildren = false;
                string label = FieldLabels.TryGetValue(it.name, out string l) ? l : it.displayName;
                var content = new GUIContent(label, it.tooltip);
                switch (it.name)
                {
                    case "animKey":
                        DrawStringWithOptions(it, content, _animNames);
                        break;
                    case "autoNextActionId":
                        DrawStringWithOptions(it, content, ActionIds());
                        break;
                    default:
                        EditorGUILayout.PropertyField(it, content, true);
                        break;
                }
            }
        }

        private string[] ActionIds()
        {
            var ids = new string[_state.entries.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = _state.entries[i].action.id;
            return ids;
        }

        /// <summary>
        /// 字符串字段 + 候选下拉 (仍可手填)
        /// </summary>
        private void DrawStringWithOptions(SerializedProperty prop, GUIContent label, string[] options)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(prop, label);
                if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    var menu = new GenericMenu();
                    SerializedProperty target = prop.Copy();
                    foreach (string option in options)
                    {
                        string value = option;
                        menu.AddItem(new GUIContent(value.Replace('/', '∕')), value == target.stringValue, () =>
                        {
                            target.serializedObject.Update();
                            target.stringValue = value;
                            if (target.serializedObject.ApplyModifiedProperties()) MarkDirty();
                            Repaint();
                        });
                    }
                    menu.ShowAsContext();
                }
            }
        }

        #endregion
    }
}
