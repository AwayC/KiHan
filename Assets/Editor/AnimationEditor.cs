using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using KiHan.Logic;

[CustomEditor(typeof(AnimationFrameData))]
public class AnimationFrameDataEditor : Editor
{
    private AnimationFrameData _data;
    private int _previewIndex = 0;
    private bool _isPlaying = false;
    private double _lastUpdateTime;

    private SerializedProperty _stepsProp;
    private SerializedProperty _libraryProp;

    private int _batchStartIndex = 0;
    private int _batchEndIndex = 0;
    private bool _showHitboxesInPreview = true;
    private float _previewZoom = 1.8f;
    private Vector2 _previewOffset = Vector2.zero;
    private bool _previewLoop = true;

    // 步骤拖动排序：按住行首手柄拖动，松开时把该步骤移动到插入线位置 (Esc 取消)
    private readonly List<Rect> _stepRowRects = new List<Rect>();
    private int _dragStep = -1;
    private int _dragSlot = -1;
    private void OnEnable()
    {
        _data = (AnimationFrameData)target;
        _stepsProp = serializedObject.FindProperty("Steps");
        _libraryProp = serializedObject.FindProperty("Library");
        _lastUpdateTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }
    private int GetTotalTicks()
    {
        int sum = 0;
        if (_data != null && _data.Steps != null)
        {
            for (int i = 0; i < _data.Steps.Count; i++) sum += Mathf.Max(1, _data.Steps[i].Duration);
        }
        return sum;
    }

    /// <summary>
    /// 指定步骤的起始逻辑帧 (从 0 计，= 之前所有步骤 Duration 之和)
    /// </summary>
    private int StepStartFrame(int stepIndex)
    {
        int sum = 0;
        for (int i = 0; i < stepIndex && i < _data.Steps.Count; i++) sum += Mathf.Max(1, _data.Steps[i].Duration);
        return sum;
    }


    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // 即使鼠标不移动，也通过 EditorApplication.update 保持 30 FPS 连续播放
        if (_isPlaying && _data != null && _data.Steps != null && _data.Steps.Count > 0)
        {
            _previewIndex = Mathf.Clamp(_previewIndex, 0, _data.Steps.Count - 1);
            int duration = Mathf.Max(1, _data.Steps[_previewIndex].Duration);
            float interval = GameConfig.RENDER_TICK_TIME * duration;

            if (EditorApplication.timeSinceStartup - _lastUpdateTime >= interval)
            {
                int nextIndex = _previewIndex + 1;
                if (nextIndex < _data.Steps.Count)
                {
                    _previewIndex = nextIndex;
                }
                else if (_previewLoop)
                {
                    _previewIndex = 0;
                }
                else
                {
                    _isPlaying = false; // 非循环动画播放完毕自动暂停
                    _previewIndex = _data.Steps.Count - 1;
                }
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AnimName"), new GUIContent("动画名称 (AnimName)"));
            EditorGUILayout.PropertyField(_libraryProp, new GUIContent("引用帧库 (SpriteLibrary)"));
            _previewLoop = EditorGUILayout.Toggle(new GUIContent("编辑器预览循环", "仅用于编辑器视口预览播放时决定是否自动循环"), _previewLoop);

            if (_data != null)
            {
                int totalTicks = GetTotalTicks();
                float totalSec = totalTicks * GameConfig.RENDER_TICK_TIME;
                EditorGUILayout.HelpBox($"动画总帧步数: {totalTicks} 帧 | 动画时长: {totalSec:F2} 秒 (30 FPS 动画时钟)", MessageType.None);
            }
        }

        if (_data.Library == null)
        {
            EditorGUILayout.HelpBox("请先指定一个 CharacterSpriteLibrary 资源，方可预览和配置动作。", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space(5);
        DrawSimplePreview();
        EditorGUILayout.Space(5);
        DrawStepList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSimplePreview()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            Rect rect = GUILayoutUtility.GetRect(0, 260, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            // 滚轮缩放与右键/中键平移视口
            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    _previewZoom = Mathf.Clamp(_previewZoom - e.delta.y * 0.08f, 0.2f, 8.0f);
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
                {
                    _previewOffset += e.delta;
                    e.Use();
                    Repaint();
                }
            }

            GUI.BeginGroup(rect);
            // 核心对齐：以脚底中心 (0, 0) 为原点基准
            Vector2 origin = new Vector2(rect.width * 0.5f, rect.height * 0.75f) + _previewOffset;

            // 绘制地平面与原点交叉辅助线
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawLine(new Vector3(0, origin.y), new Vector3(rect.width, origin.y)); // 地平线
            Handles.DrawLine(new Vector3(origin.x, 0), new Vector3(origin.x, rect.height)); // 垂直中心线

            if (_data.Steps != null && _data.Steps.Count > 0)
            {
                _previewIndex = Mathf.Clamp(_previewIndex, 0, _data.Steps.Count - 1);
                var frameData = _data.GetCurrentFrameData(_previewIndex);
                if (frameData != null && frameData.Sprite != null)
                {
                    DrawPreviewSpriteAndBoxes(origin, frameData, _previewZoom);
                }
            }
            GUI.EndGroup();

            if (_data.Steps != null && _data.Steps.Count > 0)
            {
                _previewIndex = Mathf.Clamp(_previewIndex, 0, _data.Steps.Count - 1);
                var currentStep = _data.Steps[_previewIndex];
                int startFrame = StepStartFrame(_previewIndex);
                int endFrame = startFrame + Mathf.Max(1, currentStep.Duration) - 1;
                string frameText = endFrame > startFrame ? $"{startFrame}~{endFrame}" : startFrame.ToString();
                // 帧号从 0 计，与动作配置 (frameRange / attackPhase 等) 及 FrameAnimator.ElapsedFrames 一致
                string info = $"帧: {frameText} / 共 {GetTotalTicks()} 帧 | Step: {_previewIndex + 1}/{_data.Steps.Count} | SFF:[{currentStep.FrameIndex}] | 持续:{currentStep.Duration} Ticks";
                var frameData = _data.GetCurrentFrameData(_previewIndex);
                if (frameData != null) info += $" | {frameData.Name}";

                GUI.color = Color.yellow;
                GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, 20), info, EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
            }

            // 播放与调试控制栏
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<< 上一帧", GUILayout.Height(24)))
                {
                    _previewIndex = Mathf.Max(0, _previewIndex - 1);
                }

                GUI.color = _isPlaying ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.6f);
                string playLabel = _isPlaying ? "■ 停止播放 (STOP)" : "► 播放动画 (PLAY)";
                if (GUILayout.Button(playLabel, GUILayout.Height(24)))
                {
                    _isPlaying = !_isPlaying;
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                }
                GUI.color = Color.white;

                if (GUILayout.Button("下一帧 >>", GUILayout.Height(24)))
                {
                    if (_data.Steps != null && _data.Steps.Count > 0)
                    {
                        _previewIndex = (_previewIndex + 1) % _data.Steps.Count;
                    }
                }

                _showHitboxesInPreview = GUILayout.Toggle(_showHitboxesInPreview, "显示判定盒", "Button", GUILayout.Width(80), GUILayout.Height(24));

                _previewZoom = EditorGUILayout.Slider("缩放", _previewZoom, 0.5f, 6.0f, GUILayout.Width(180));
                if (GUILayout.Button("复位", GUILayout.Width(45), GUILayout.Height(24)))
                {
                    _previewZoom = 1.8f;
                    _previewOffset = Vector2.zero;
                }
            }
            // 可拖动的动画进度条 (Timeline Scrubber)
            if (_data.Steps != null && _data.Steps.Count > 0)
            {
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    EditorGUILayout.LabelField("进度", EditorStyles.miniBoldLabel, GUILayout.Width(32));

                    int currentTicks = 0;
                    for (int t = 0; t <= _previewIndex && t < _data.Steps.Count; t++)
                    {
                        currentTicks += Mathf.Max(1, _data.Steps[t].Duration);
                    }
                    float curSec = currentTicks * GameConfig.RENDER_TICK_TIME;
                    float totalSec = GetTotalTicks() * GameConfig.RENDER_TICK_TIME;

                    int displayStep = _previewIndex + 1;
                    int newStep = EditorGUILayout.IntSlider(displayStep, 1, _data.Steps.Count);
                    if (newStep != displayStep)
                    {
                        _previewIndex = newStep - 1;
                        _lastUpdateTime = EditorApplication.timeSinceStartup;
                    }

                    EditorGUILayout.LabelField($"{curSec:F2}s / {totalSec:F2}s (#{displayStep})", EditorStyles.miniLabel, GUILayout.Width(110));
                }
            }
        }
    }

    private void DrawPreviewSpriteAndBoxes(Vector2 origin, SpriteFrameData frameData, float zoom)
    {
        Sprite sprite = frameData.Sprite;
        if (sprite == null || sprite.texture == null) return;

        Rect sRect = sprite.textureRect;
        if (sRect.height <= 0) return;

        float w = sRect.width * zoom;
        float h = sRect.height * zoom;

        // 核心对齐：与 SpriteLibraryEditor 保持完全一致的 Offset 像素空间对齐
        Rect drawRect = new Rect(
            origin.x + frameData.Offset.x * zoom - w * 0.5f,
            origin.y - frameData.Offset.y * zoom - h * 0.5f,
            w,
            h
        );
        Rect uv = new Rect(sRect.x / sprite.texture.width, sRect.y / sprite.texture.height, sRect.width / sprite.texture.width, sRect.height / sprite.texture.height);

        // 1. 绘制精灵本体
        GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uv, true);

        // 2. 绘制附带图层 (如刀光气劲)
        if (frameData.ExtraLayers != null)
        {
            foreach (var layer in frameData.ExtraLayers)
            {
                if (layer.Sprite == null || layer.Sprite.texture == null) continue;
                Rect lRect = layer.Sprite.textureRect;
                float lw = lRect.width * zoom;
                float lh = lRect.height * zoom;
                Vector2 totalOffset = frameData.Offset + layer.Offset;
                Rect lDrawRect = new Rect(
                    origin.x + totalOffset.x * zoom - lw * 0.5f,
                    origin.y - totalOffset.y * zoom - lh * 0.5f,
                    lw,
                    lh
                );
                Rect lUv = new Rect(lRect.x / layer.Sprite.texture.width, lRect.y / layer.Sprite.texture.height, lRect.width / layer.Sprite.texture.width, lRect.height / layer.Sprite.texture.height);
                Color oldC = GUI.color;
                GUI.color = layer.TintColor;
                GUI.DrawTextureWithTexCoords(lDrawRect, layer.Sprite.texture, lUv, true);
                GUI.color = oldC;
            }
        }

        // 3. 绘制受击盒与攻击盒 (与 SpriteLibraryEditor 的 DrawLogicBox 算法完全一致)
        if (_showHitboxesInPreview)
        {
            // 受击盒 (蓝框)
            var hurtBoxes = _data.GetHurtBoxes(_previewIndex);
            if (hurtBoxes != null)
            {
                foreach (var b in hurtBoxes)
                {
                    DrawLogicBox(origin, b, new Color(0f, 0.5f, 1f, 0.35f), Color.cyan, zoom);
                }
            }

            // 攻击盒 (红框)
            var hitBoxes = _data.GetHitBoxes(_previewIndex);
            if (hitBoxes != null)
            {
                foreach (var b in hitBoxes)
                {
                    DrawLogicBox(origin, b, new Color(1f, 0f, 0f, 0.4f), Color.red, zoom);
                }
            }
        }
    }

    private void DrawLogicBox(Vector2 origin, LogicBox b, Color fill, Color outline, float zoom)
    {
        if (b.Size.x <= 0 || b.Size.y <= 0) return;
        Rect r = new Rect(
            origin.x + (b.Center.x - b.Size.x * 0.5f) * zoom,
            origin.y - (b.Center.y + b.Size.y * 0.5f) * zoom,
            b.Size.x * zoom,
            b.Size.y * zoom
        );
        EditorGUI.DrawRect(r, fill);
        Handles.color = outline;
        Handles.DrawSolidRectangleWithOutline(r, Color.clear, outline);
    }

    private void DrawStepList()
    {
        EditorGUILayout.LabelField("动画步骤序列 (Animation Steps)", EditorStyles.boldLabel);

        if (_stepsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前动作序列为空，请点击下方按钮添加步骤或批量导入帧区间。", MessageType.Info);
        }

        int deleteIndex = -1;
        int dragControl = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        while (_stepRowRects.Count < _stepsProp.arraySize) _stepRowRects.Add(Rect.zero);
        if (_stepRowRects.Count > _stepsProp.arraySize) _stepRowRects.RemoveRange(_stepsProp.arraySize, _stepRowRects.Count - _stepsProp.arraySize);

        int frameCursor = 0;
        for (int i = 0; i < _stepsProp.arraySize; i++)
        {
            SerializedProperty stepProp = _stepsProp.GetArrayElementAtIndex(i);
            bool isCurrent = (i == _previewIndex);

            Color originalColor = GUI.backgroundColor;
            if (i == _dragStep) GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            else if (isCurrent) GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = originalColor;

                using (new EditorGUILayout.HorizontalScope())
                {
                    // 拖动手柄
                    Rect handle = GUILayoutUtility.GetRect(14, 18, GUILayout.Width(14));
                    EditorGUIUtility.AddCursorRect(handle, MouseCursor.Pan);
                    GUI.Label(handle, "≡", EditorStyles.centeredGreyMiniLabel);
                    if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
                    {
                        _dragStep = i;
                        _dragSlot = i;
                        GUIUtility.hotControl = dragControl;
                        e.Use();
                    }

                    // 步骤序号
                    EditorGUILayout.LabelField($"#{i + 1}", EditorStyles.boldLabel, GUILayout.Width(28));

                    // 本步骤覆盖的逻辑帧区间 (从 0 计)
                    int duration = Mathf.Max(1, stepProp.FindPropertyRelative("Duration").intValue);
                    int lastFrame = frameCursor + duration - 1;
                    EditorGUILayout.LabelField(lastFrame > frameCursor ? $"帧 {frameCursor}~{lastFrame}" : $"帧 {frameCursor}", EditorStyles.miniLabel, GUILayout.Width(62));
                    frameCursor += duration;

                    // 切片帧按钮 (支持弹出搜索选择)
                    int fIdx = stepProp.FindPropertyRelative("FrameIndex").intValue;
                    string frameName = "NONE";
                    if (_data.Library != null && fIdx >= 0 && fIdx < _data.Library.AllFrames.Count)
                    {
                        frameName = _data.Library.AllFrames[fIdx].Name;
                    }

                    if (GUILayout.Button($"[{fIdx}] {frameName}", EditorStyles.popup, GUILayout.MinWidth(140)))
                    {
                        Rect btnRect = GUILayoutUtility.GetLastRect();
                        SerializedProperty frameIndexProp = stepProp.FindPropertyRelative("FrameIndex");
                        ShowFrameSelectionMenu(frameIndexProp, btnRect);
                    }

                    // 持续帧数 Duration
                    EditorGUILayout.LabelField("Ticks:", GUILayout.Width(38));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("Duration"), GUIContent.none, GUILayout.Width(40));


                    // 移动与删除操作按钮
                    EditorGUI.BeginDisabledGroup(i == 0);
                    if (GUILayout.Button("▲", GUILayout.Width(24)))
                    {
                        _stepsProp.MoveArrayElement(i, i - 1);
                        break;
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(i == _stepsProp.arraySize - 1);
                    if (GUILayout.Button("▼", GUILayout.Width(24)))
                    {
                        _stepsProp.MoveArrayElement(i, i + 1);
                        break;
                    }
                    EditorGUI.EndDisabledGroup();

                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        deleteIndex = i;
                    }
                    GUI.color = Color.white;
                }

            }
            if (e.type == EventType.Repaint && i < _stepRowRects.Count) _stepRowRects[i] = GUILayoutUtility.GetLastRect();
        }

        HandleStepDrag(dragControl);

        // 安全执行删除，防止越界异常
        if (deleteIndex >= 0)
        {
            _stepsProp.DeleteArrayElementAtIndex(deleteIndex);
            if (_data.Steps.Count > 0)
            {
                _previewIndex = Mathf.Clamp(_previewIndex, 0, _data.Steps.Count - 1);
            }
            else
            {
                _previewIndex = 0;
            }
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("+ 添加新动作步骤 (Add Step)", GUILayout.Height(30)))
        {
            _stepsProp.InsertArrayElementAtIndex(_stepsProp.arraySize);
            var newStep = _stepsProp.GetArrayElementAtIndex(_stepsProp.arraySize - 1);
            newStep.FindPropertyRelative("Duration").intValue = 2;
        }

        // 批量添加
        EditorGUILayout.Space(8);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("批量快速导入连续帧区间 (Batch Import Range)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("起始帧:", GUILayout.Width(45));
                _batchStartIndex = EditorGUILayout.IntField(_batchStartIndex, GUILayout.Width(45));
                EditorGUILayout.LabelField("终止帧:", GUILayout.Width(45));
                _batchEndIndex = EditorGUILayout.IntField(_batchEndIndex, GUILayout.Width(45));

                if (GUILayout.Button("批量追加到步骤", GUILayout.Height(20)))
                {
                    int maxIdx = (_data.Library != null) ? _data.Library.AllFrames.Count - 1 : 9999;
                    int start = Mathf.Clamp(_batchStartIndex, 0, maxIdx);
                    int end = Mathf.Clamp(_batchEndIndex, 0, maxIdx);

                    int dir = start <= end ? 1 : -1;
                    for (int i = start; dir > 0 ? i <= end : i >= end; i += dir)
                    {
                        _stepsProp.arraySize++;
                        var newElement = _stepsProp.GetArrayElementAtIndex(_stepsProp.arraySize - 1);
                        newElement.FindPropertyRelative("FrameIndex").intValue = i;
                        newElement.FindPropertyRelative("Duration").intValue = 2;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 拖动中：按鼠标高度计算插入位置并画插入线；松开时移动步骤，预览跟随被移动的步骤
    /// </summary>
    private void HandleStepDrag(int dragControl)
    {
        if (_dragStep < 0) return;
        Event e = Event.current;
        int count = _stepRowRects.Count;
        if (_dragStep >= count)
        {
            EndStepDrag();
            return;
        }

        switch (e.GetTypeForControl(dragControl))
        {
            case EventType.MouseDrag:
                _dragSlot = count;
                for (int k = 0; k < count; k++)
                {
                    if (e.mousePosition.y < _stepRowRects[k].center.y) { _dragSlot = k; break; }
                }
                e.Use();
                Repaint();
                break;

            case EventType.MouseUp:
                int to = _dragSlot > _dragStep ? _dragSlot - 1 : _dragSlot;
                if (to >= 0 && to != _dragStep)
                {
                    _stepsProp.MoveArrayElement(_dragStep, to);
                    _previewIndex = to;
                }
                EndStepDrag();
                e.Use();
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.Escape)
                {
                    EndStepDrag();
                    e.Use();
                }
                break;

            case EventType.Repaint:
                if (_dragSlot < 0 || _dragSlot == _dragStep || _dragSlot == _dragStep + 1) break;
                Rect row = _stepRowRects[Mathf.Min(_dragSlot, count - 1)];
                float y = _dragSlot < count ? row.yMin - 2f : row.yMax + 1f;
                EditorGUI.DrawRect(new Rect(row.x, y, row.width, 2f), new Color(0.3f, 0.65f, 1f));
                break;
        }
    }

    private void EndStepDrag()
    {
        _dragStep = -1;
        _dragSlot = -1;
        if (GUIUtility.hotControl != 0) GUIUtility.hotControl = 0;
        Repaint();
    }

    private void ShowFrameSelectionMenu(SerializedProperty frameIndexProp, Rect buttonRect)
    {
        if (_data.Library == null) return;
        PopupWindow.Show(buttonRect, new FrameSelectionPopup(_data.Library, frameIndexProp, serializedObject));
    }
}

public class FrameSelectionPopup : PopupWindowContent
{
    private CharacterSpriteLibrary _library;
    private SerializedProperty _frameIndexProp;
    private SerializedObject _serializedObject;
    private string _searchString = "";
    private Vector2 _scrollPos;

    public FrameSelectionPopup(CharacterSpriteLibrary library, SerializedProperty frameIndexProp, SerializedObject serializedObject)
    {
        _library = library;
        _frameIndexProp = frameIndexProp;
        _serializedObject = serializedObject;
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(320, 420);
    }

    public override void OnGUI(Rect rect)
    {
        EditorGUILayout.LabelField("选择库帧 (Select Sprite Frame)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20))) _searchString = "";
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        if (_library != null && _library.AllFrames != null)
        {
            for (int i = 0; i < _library.AllFrames.Count; i++)
            {
                var f = _library.AllFrames[i];
                if (!string.IsNullOrEmpty(_searchString) && !f.Name.ToLower().Contains(_searchString.ToLower()))
                {
                    continue;
                }

                if (GUILayout.Button($"[{i:D3}] {f.Name}", EditorStyles.miniButtonLeft))
                {
                    _serializedObject.Update();
                    _frameIndexProp.intValue = i;
                    _serializedObject.ApplyModifiedProperties();
                    editorWindow.Close();
                    break;
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
