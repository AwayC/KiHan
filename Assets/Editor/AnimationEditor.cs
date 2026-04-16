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

    private void OnEnable()
    {
        _data = (AnimationFrameData)target;
        _stepsProp = serializedObject.FindProperty("Steps");
        _libraryProp = serializedObject.FindProperty("Library");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

<<<<<<< Updated upstream
        if (_data.Frames == null || _data.Frames.Count == 0) return;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("动作预览 (支持 Duration 停留帧)", EditorStyles.boldLabel);

        // --- 预览计时逻辑 ---
        float baseInterval = 1f / Mathf.Max(1f, _data.FrameRate);
        double currentTime = EditorApplication.timeSinceStartup;

        if (!_isPaused && Application.isPlaying == false)
=======
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
>>>>>>> Stashed changes
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

        Rect drawRect = new Rect(rect.center.x - w/2, rect.center.y - h/2, w, h);
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
                    ShowFrameSelectionMenu(i);
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

<<<<<<< Updated upstream
        // --- 绘制区域 ---
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            Rect rect = GUILayoutUtility.GetRect(0, 250, GUILayout.ExpandWidth(true));
            var frame = _data.Frames[_previewIndex];
            if (frame.Sprite != null) DrawSpriteIndustrial(rect, frame.Sprite);

            // 进度控制
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPaused ? "播放" : "暂停")) _isPaused = !_isPaused;
            EditorGUI.BeginChangeCheck();
            _previewIndex = EditorGUILayout.IntSlider(_previewIndex, 0, _data.Frames.Count - 1);
            if (EditorGUI.EndChangeCheck()) { _isPaused = true; Repaint(); }
            EditorGUILayout.EndHorizontal();
=======
        EditorGUILayout.Space(5);
        if (GUILayout.Button("+ 添加新步骤 (ADD STEP)", GUILayout.Height(35))) 
        {
            _stepsProp.InsertArrayElementAtIndex(_stepsProp.arraySize);
>>>>>>> Stashed changes
        }
    }

    private void ShowFrameSelectionMenu(int stepIndex)
    {
        GenericMenu menu = new GenericMenu();
        if (_data.Library == null) return;

        for (int j = 0; j < _data.Library.AllFrames.Count; j++)
        {
            int frameIndex = j;
            string name = _data.Library.AllFrames[j].Name;
            
            menu.AddItem(new GUIContent($"{j}: {name}"), false, () => {
                Undo.RecordObject(_data, "Change Anim Step Sprite");
                if (stepIndex < _data.Steps.Count)
                {
                    _data.Steps[stepIndex].FrameIndex = frameIndex;
                    EditorUtility.SetDirty(_data);
                }
            });
        }
        menu.ShowAsContext();
    }
}
