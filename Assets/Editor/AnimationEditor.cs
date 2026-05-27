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

    private void OnEnable()
    {
        _data = (AnimationFrameData)target;
        _stepsProp = serializedObject.FindProperty("Steps");
        _libraryProp = serializedObject.FindProperty("Library");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AnimName"));
            EditorGUILayout.PropertyField(_libraryProp, new GUIContent("引用帧库 (SFF)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IsLoop"));
        }

        if (_data.Library == null)
        {
            EditorGUILayout.HelpBox("请先指定一个 CharacterSpriteLibrary 资源。", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space(10);
        DrawSimplePreview();
        DrawStepList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSimplePreview()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            Rect rect = GUILayoutUtility.GetRect(0, 200, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1));

            if (_data.Steps != null && _data.Steps.Count > 0)
            {
                _previewIndex = Mathf.Clamp(_previewIndex, 0, _data.Steps.Count - 1);

                // 显示当前帧信息
                var currentStep = _data.Steps[_previewIndex];
                string info = $"Step: {_previewIndex + 1} / {_data.Steps.Count} (SFF: {currentStep.FrameIndex})";

                var frameData = _data.GetCurrentFrameData(_previewIndex);
                if (frameData != null && frameData.Sprite != null)
                {
                    DrawPreviewSprite(rect, frameData.Sprite);
                    info += $" - {frameData.Name}";
                }

                GUI.color = Color.yellow;
                GUI.Label(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 20), info, EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
            }

            // 播放控制
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<<")) _previewIndex = Mathf.Max(0, _previewIndex - 1);
            _isPlaying = GUILayout.Toggle(_isPlaying, _isPlaying ? "STOP" : "PLAY", "Button");
            if (GUILayout.Button(">>")) _previewIndex = (_previewIndex + 1) % Mathf.Max(1, _data.Steps.Count);
            EditorGUILayout.EndHorizontal();

            if (_isPlaying && !Application.isPlaying && _data.Steps != null && _data.Steps.Count > 0)
            {
                _previewIndex = Mathf.Clamp(_previewIndex, 0, _data.Steps.Count - 1);
                int duration = Mathf.Max(1, _data.Steps[_previewIndex].Duration);
                float interval = 0.066f * duration;
                if (EditorApplication.timeSinceStartup - _lastUpdateTime > interval)
                {
                    _previewIndex = (_previewIndex + 1) % _data.Steps.Count;
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                    Repaint();
                }
            }
        }
    }

    private void DrawPreviewSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;
        Rect sRect = sprite.textureRect;
        if (sRect.height <= 0) return;

        float ratio = sRect.width / sRect.height;
        float h = rect.height * 0.8f;
        float w = h * ratio;
        if (w > rect.width * 0.8f) { w = rect.width * 0.8f; h = w / ratio; }

        Rect drawRect = new Rect(rect.center.x - w / 2, rect.center.y - h / 2, w, h);
        Rect uv = new Rect(sRect.x / sprite.texture.width, sRect.y / sprite.texture.height, sRect.width / sprite.texture.width, sRect.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uv, true);
    }

    private void DrawStepList()
    {
        EditorGUILayout.LabelField("动画步骤序列 (AIR)", EditorStyles.boldLabel);

        if (_stepsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前列表为空，点击下方按钮添加步骤。", MessageType.Info);
        }

        for (int i = 0; i < _stepsProp.arraySize; i++)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                // --- 1. 移动按钮组 (加大加宽) ---
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(40)))
                {
                    EditorGUI.BeginDisabledGroup(i == 0);
                    if (GUILayout.Button("▲", GUILayout.Width(40), GUILayout.Height(20)))
                    {
                        _stepsProp.MoveArrayElement(i, i - 1);
                        break;
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(i == _stepsProp.arraySize - 1);
                    if (GUILayout.Button("▼", GUILayout.Width(40), GUILayout.Height(20)))
                    {
                        _stepsProp.MoveArrayElement(i, i + 1);
                        break;
                    }
                    EditorGUI.EndDisabledGroup();
                }

                SerializedProperty p = _stepsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.LabelField($"{i}", GUILayout.Width(15));

                int fIdx = p.FindPropertyRelative("FrameIndex").intValue;
                string frameName = "NONE";
                if (_data.Library != null && fIdx >= 0 && fIdx < _data.Library.AllFrames.Count)
                    frameName = _data.Library.AllFrames[fIdx].Name;

                if (GUILayout.Button($"{fIdx}: {frameName}", EditorStyles.layerMaskField, GUILayout.ExpandWidth(true)))
                {
                    Rect btnRect = GUILayoutUtility.GetLastRect();
                    SerializedProperty frameIndexProp = p.FindPropertyRelative("FrameIndex");
                    ShowFrameSelectionMenu(frameIndexProp, btnRect);
                }

                EditorGUILayout.PropertyField(p.FindPropertyRelative("Duration"), GUIContent.none, GUILayout.Width(35));
                EditorGUILayout.PropertyField(p.FindPropertyRelative("RootMotion"), GUIContent.none, GUILayout.Width(80));

                GUI.color = new Color(1, 0.5f, 0.5f);
                if (GUILayout.Button("DEL", GUILayout.Width(40), GUILayout.Height(42)))
                {
                    _stepsProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                GUI.color = Color.white;
            }
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("+ 添加新步骤 (ADD STEP)", GUILayout.Height(35)))
        {
            _stepsProp.InsertArrayElementAtIndex(_stepsProp.arraySize);
        }

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("批量添加帧区间", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Start:", GUILayout.Width(40));
                _batchStartIndex = EditorGUILayout.IntField(_batchStartIndex, GUILayout.Width(50));
                EditorGUILayout.LabelField("End:", GUILayout.Width(30));
                _batchEndIndex = EditorGUILayout.IntField(_batchEndIndex, GUILayout.Width(50));
                if (GUILayout.Button("批量导入区间", GUILayout.Height(20)))
                {
                    int maxIdx = (_data.Library != null) ? _data.Library.AllFrames.Count - 1 : 9999;
                    int start = Mathf.Clamp(_batchStartIndex, 0, maxIdx);
                    int end = Mathf.Clamp(_batchEndIndex, 0, maxIdx);
                    
                    int step = start <= end ? 1 : -1;
                    for (int i = start; step > 0 ? i <= end : i >= end; i += step)
                    {
                        _stepsProp.arraySize++;
                        var newElement = _stepsProp.GetArrayElementAtIndex(_stepsProp.arraySize - 1);
                        newElement.FindPropertyRelative("FrameIndex").intValue = i;
                        newElement.FindPropertyRelative("Duration").intValue = 1;
                        newElement.FindPropertyRelative("RootMotion").vector2Value = Vector2.zero;
                    }
                }
            }
        }
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
        return new Vector2(300, 400);
    }

    public override void OnGUI(Rect rect)
    {
        GUILayout.Space(5);
        GUILayout.Label("选择帧", EditorStyles.boldLabel);
        
        // Search bar
        EditorGUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
        _searchString = GUILayout.TextField(_searchString, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarSearchField);
        if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? EditorStyles.toolbarButton))
        {
            _searchString = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        // List
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        for (int i = 0; i < _library.AllFrames.Count; i++)
        {
            string frameName = _library.AllFrames[i].Name;
            
            if (!string.IsNullOrEmpty(_searchString) && !frameName.ToLower().Contains(_searchString.ToLower()))
            {
                continue;
            }

            if (GUILayout.Button($"{i}: {frameName}", EditorStyles.miniButtonLeft))
            {
                _serializedObject.Update();
                _frameIndexProp.intValue = i;
                _serializedObject.ApplyModifiedProperties();
                editorWindow.Close();
            }
        }
        EditorGUILayout.EndScrollView();
    }
}