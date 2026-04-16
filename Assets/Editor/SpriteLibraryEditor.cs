using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using KiHan.Logic;
using System.Linq;

[CustomEditor(typeof(CharacterSpriteLibrary))]
public class SpriteLibraryEditor : Editor
{
    private CharacterSpriteLibrary _lib;
    private int _currentIndex = 0;
    private SerializedProperty _framesProp;
    private SerializedProperty _defHurtProp;
    private SerializedProperty _defHitProp;
    private SerializedProperty _presetsProp;

    private Vector2 _viewOffset = Vector2.zero;
    private float _viewZoom = 2.0f;
    private float _previewAlpha = 1.0f;
    private bool _showOnionSkin = true;

    private string _searchString = "";
    private Vector2 _listScroll;

    private int _editMode = 0; 
    private Vector2 _dragStartPos;
    private int _activeBoxIndex = -1;
    private int _activePresetIndex = 0; 

    private void OnEnable()
    {
        _lib = (CharacterSpriteLibrary)target;
        _framesProp = serializedObject.FindProperty("AllFrames");
        _defHurtProp = serializedObject.FindProperty("DefaultHurtBox");
        _defHitProp = serializedObject.FindProperty("DefaultHitBox");
        _presetsProp = serializedObject.FindProperty("SidePresets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDropZone();
        EditorGUILayout.Space(5);
        DrawSearchAndList();
        EditorGUILayout.Space(5);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("厚度预设与全局套用", EditorStyles.boldLabel);
            for (int i = 0; i < _presetsProp.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(_presetsProp.GetArrayElementAtIndex(i), GUIContent.none);
                    float val = _presetsProp.GetArrayElementAtIndex(i).floatValue;
                    if (GUILayout.Button($"全局套用", GUILayout.Width(70))) ApplySideToAll(val);
                    if (GUILayout.Button("设为默认", GUILayout.Width(70))) _activePresetIndex = i;
                    if (GUILayout.Button("×", GUILayout.Width(20))) { _presetsProp.DeleteArrayElementAtIndex(i); break; }
                }
            }
            if (GUILayout.Button("+ 添加新预设")) _presetsProp.InsertArrayElementAtIndex(_presetsProp.arraySize);
        }

        if (_framesProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("帧库为空，请拖拽导入。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        _currentIndex = Mathf.Clamp(_currentIndex, 0, _framesProp.arraySize - 1);

        EditorGUILayout.Space(5);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showOnionSkin = EditorGUILayout.Toggle("洋葱皮", _showOnionSkin);
                _previewAlpha = EditorGUILayout.Slider("透明度", _previewAlpha, 0.1f, 1.0f);
            }
            _viewZoom = EditorGUILayout.Slider("缩放", _viewZoom, 0.1f, 10f);
            if (GUILayout.Button("重置视图")) { _viewOffset = Vector2.zero; _viewZoom = 2.0f; }
        }

        DrawPreviewArea();
        DrawFrameDetails();
        DrawNavigator();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSearchAndList()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("帧列表快速定位", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20))) _searchString = "";
            }
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(120));
            for (int i = 0; i < _framesProp.arraySize; i++)
            {
                string fName = _framesProp.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;
                if (!string.IsNullOrEmpty(_searchString) && !fName.ToLower().Contains(_searchString.ToLower())) continue;
                
                GUI.color = (i == _currentIndex) ? Color.cyan : Color.white;
                if (GUILayout.Button($"{i:D3}: {fName}", EditorStyles.miniButtonLeft)) _currentIndex = i;
            }
            GUI.color = Color.white;
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawPreviewArea()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            Rect rect = GUILayoutUtility.GetRect(0, 400, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1));
            GUI.BeginGroup(rect);
            Vector2 localCenter = rect.center + _viewOffset;

            Handles.color = new Color(1, 1, 1, 0.15f);
            Handles.DrawLine(new Vector3(0, localCenter.y), new Vector3(rect.width, localCenter.y));
            Handles.DrawLine(new Vector3(localCenter.x, 0), new Vector3(localCenter.x, rect.height));

            var frame = _lib.AllFrames[_currentIndex];
            if (_showOnionSkin && _currentIndex > 0)
                DrawOneSprite(localCenter, _lib.AllFrames[_currentIndex-1].Sprite, _lib.AllFrames[_currentIndex-1].Offset, _viewZoom, 0.25f, Color.white);

            if (frame.Sprite != null)
            {
                // 1. 渲染本体
                DrawOneSprite(localCenter, frame.Sprite, frame.Offset, _viewZoom, _previewAlpha, Color.white);
                
                // 2. 渲染特效层 (确保显示在本体之上)
                foreach (var layer in frame.ExtraLayers)
                {
                    if (layer.Sprite == null) continue;
                    // 即使 TintColor 为 (0,0,0,0)，我们也强行保证预览可见（如果是刚创建的）
                    Color drawColor = layer.TintColor;
                    if (drawColor.a == 0 && drawColor.r == 0 && drawColor.g == 0 && drawColor.b == 0) drawColor = Color.white;
                    
                    DrawOneSprite(localCenter, layer.Sprite, frame.Offset + layer.Offset, _viewZoom, _previewAlpha * drawColor.a, drawColor);
                }
                
                foreach (var b in frame.HurtBoxes) DrawLogicBox(localCenter, b, Color.blue, _viewZoom);
                foreach (var b in frame.HitBoxes) DrawLogicBox(localCenter, b, Color.red, _viewZoom);
            }
            GUI.EndGroup();
            HandleInput(rect, localCenter, _viewZoom);
        }
    }

    private void DrawOneSprite(Vector2 center, Sprite s, Vector2 off, float z, float a, Color t)
    {
        if (s == null || s.texture == null) return;
        Rect sr = s.textureRect;
        float w = sr.width * z, h = sr.height * z;
        Rect dr = new Rect(center.x + off.x * z - w/2, center.y - off.y * z - h/2, w, h);
        Rect uv = new Rect(sr.x / s.texture.width, sr.y / s.texture.height, sr.width / s.texture.width, sr.height / s.texture.height);
        Color old = GUI.color; GUI.color = new Color(t.r, t.g, t.b, a);
        GUI.DrawTextureWithTexCoords(dr, s.texture, uv, true);
        GUI.color = old;
    }

    private void DrawLogicBox(Vector2 center, LogicBox b, Color c, float z)
    {
        if (b.Size.x <= 0 || b.Size.y <= 0) return;
        Rect r = new Rect(center.x + b.Center.x * z - b.Size.x * z / 2, center.y - b.Center.y * z - b.Size.y * z / 2, b.Size.x * z, b.Size.y * z);
        EditorGUI.DrawRect(r, new Color(c.r, c.g, c.b, 0.3f));
        Handles.color = c; Handles.DrawSolidRectangleWithOutline(r, new Color(0,0,0,0), c);
    }

    private void HandleInput(Rect screenRect, Vector2 localCenter, float zoom)
    {
        Event e = Event.current;
        if (_currentIndex >= _lib.AllFrames.Count) return;
        var frame = _lib.AllFrames[_currentIndex];

        if (screenRect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
        {
            _viewZoom = Mathf.Clamp(_viewZoom - e.delta.y * 0.05f, 0.1f, 10f); e.Use(); Repaint();
        }

        Vector2 mouseLogicPos = (e.mousePosition - (screenRect.position + localCenter)) / zoom;
        mouseLogicPos.y = -mouseLogicPos.y; 

        if (e.type == EventType.MouseDown)
        {
            if (e.shift)
            {
                _editMode = (e.button == 0) ? 1 : 2; 
                Undo.RecordObject(_lib, "Add Box");
                float side = (_lib.SidePresets.Count > _activePresetIndex) ? _lib.SidePresets[_activePresetIndex] : 1.0f;
                var nb = new LogicBox(mouseLogicPos, Vector2.zero, side);
                if (_editMode == 1) { frame.HurtBoxes.Add(nb); _activeBoxIndex = frame.HurtBoxes.Count-1; }
                else { frame.HitBoxes.Add(nb); _activeBoxIndex = frame.HitBoxes.Count-1; }
            }
            else if (e.control && e.button == 0) { _editMode = 3; Undo.RecordObject(_lib, "Move Sprite"); }
            else if (e.button == 1 || e.button == 2) _editMode = 4;
            else _editMode = 0;

            if (_editMode != 0) { _dragStartPos = (_editMode == 4) ? e.mousePosition : mouseLogicPos; e.Use(); }
        }
        else if (e.type == EventType.MouseDrag && _editMode != 0)
        {
            if (_editMode == 4) { _viewOffset += (e.mousePosition - _dragStartPos); _dragStartPos = e.mousePosition; }
            else if (_editMode == 3) frame.Offset = mouseLogicPos; 
            else 
            {
                Vector2 center = (_dragStartPos + mouseLogicPos) / 2f;
                Vector2 size = new Vector2(Mathf.Abs(_dragStartPos.x - mouseLogicPos.x), Mathf.Abs(_dragStartPos.y - mouseLogicPos.y));
                if (_editMode == 1) { var b = frame.HurtBoxes[_activeBoxIndex]; b.Center = center; b.Size = size; frame.HurtBoxes[_activeBoxIndex] = b; }
                else { var b = frame.HitBoxes[_activeBoxIndex]; b.Center = center; b.Size = size; frame.HitBoxes[_activeBoxIndex] = b; }
            }
            e.Use(); Repaint();
        }
        else if (e.type == EventType.MouseUp) _editMode = 0;
    }

    private void DrawFrameDetails()
    {
        if (_currentIndex < 0 || _currentIndex >= _framesProp.arraySize) return;
        var p = _framesProp.GetArrayElementAtIndex(_currentIndex);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField($"帧详情 [{_currentIndex}] : {p.FindPropertyRelative("Name").stringValue}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Sprite"), new GUIContent("人物图片"));
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Offset"), new GUIContent("对齐偏移"));
            
            DrawCustomBoxList(p.FindPropertyRelative("HurtBoxes"), "受击盒 (蓝)");
            DrawCustomBoxList(p.FindPropertyRelative("HitBoxes"), "攻击盒 (红)");
            
            // --- 升级后的特效图层列表 ---
            DrawEffectLayerList(p.FindPropertyRelative("ExtraLayers"));
        }
    }

    private void DrawEffectLayerList(SerializedProperty listProp)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("同步特效层 (刀光/气劲)", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newLayer = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newLayer.FindPropertyRelative("TintColor").colorValue = Color.white; // 强制设为白且不透明
                newLayer.FindPropertyRelative("OrderOffset").intValue = 1;
                newLayer.FindPropertyRelative("Offset").vector2Value = Vector2.zero;
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty layer = listProp.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(layer.FindPropertyRelative("Sprite"), GUIContent.none);
                        if (GUILayout.Button("×", GUILayout.Width(20))) { listProp.DeleteArrayElementAtIndex(i); break; }
                    }
                    EditorGUILayout.PropertyField(layer.FindPropertyRelative("Offset"), new GUIContent("位置偏移"));
                    EditorGUILayout.PropertyField(layer.FindPropertyRelative("TintColor"), new GUIContent("颜色叠加(默认白)"));
                    EditorGUILayout.PropertyField(layer.FindPropertyRelative("OrderOffset"), new GUIContent("层级顺序"));
                }
            }
        }
    }

    private void DrawCustomBoxList(SerializedProperty listProp, string label)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int i = 0; i < listProp.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty boxProp = listProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(boxProp.FindPropertyRelative("Center"), GUIContent.none, GUILayout.Width(100));
                    EditorGUILayout.PropertyField(boxProp.FindPropertyRelative("Size"), GUIContent.none, GUILayout.Width(100));
                    float currentSide = boxProp.FindPropertyRelative("Side").floatValue;
                    if (EditorGUILayout.DropdownButton(new GUIContent(currentSide.ToString("F1")), FocusType.Passive, GUILayout.Width(40)))
                    {
                        GenericMenu menu = new GenericMenu();
                        foreach (float preset in _lib.SidePresets)
                        {
                            float val = preset;
                            menu.AddItem(new GUIContent(val.ToString("F1")), val == currentSide, () => {
                                boxProp.FindPropertyRelative("Side").floatValue = val;
                                serializedObject.ApplyModifiedProperties();
                            });
                        }
                        menu.ShowAsContext();
                    }
                    if (GUILayout.Button("×", GUILayout.Width(20))) { listProp.DeleteArrayElementAtIndex(i); break; }
                }
            }
        }
    }

    private void DrawNavigator()
    {
        using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
        {
            if (GUILayout.Button("<<")) _currentIndex = Mathf.Max(0, _currentIndex - 1);
            EditorGUILayout.LabelField($"{_currentIndex + 1} / {_framesProp.arraySize}", EditorStyles.centeredGreyMiniLabel);
            if (GUILayout.Button(">>")) _currentIndex = Mathf.Min(_framesProp.arraySize - 1, _currentIndex + 1);
            if (GUILayout.Button("删除此帧", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("删除", "确定删除吗？", "确定", "取消"))
                {
                    _framesProp.DeleteArrayElementAtIndex(_currentIndex);
                    _currentIndex = Mathf.Clamp(0, 0, _framesProp.arraySize - 1);
                }
            }
        }
    }

    private void ApplySideToAll(float side)
    {
        if (!EditorUtility.DisplayDialog("修改", $"统一修改厚度为 {side:F1}?", "是", "否")) return;
        Undo.RecordObject(_lib, "Global Apply Side");
        foreach (var f in _lib.AllFrames)
        {
            for (int j = 0; j < f.HurtBoxes.Count; j++) { var b = f.HurtBoxes[j]; b.Side = side; f.HurtBoxes[j] = b; }
            for (int j = 0; j < f.HitBoxes.Count; j++) { var b = f.HitBoxes[j]; b.Side = side; f.HitBoxes[j] = b; }
        }
        EditorUtility.SetDirty(_lib);
    }

    private void DrawDropZone()
    {
        Event e = Event.current;
        Rect dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "拖拽 Sprite 或 文件夹 导入", EditorStyles.helpBox);
        if (dropRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; e.Use(); }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(_lib, "Import");
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is Sprite s) AddToLib(s);
                    else if (obj is Texture2D t)
                        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)))
                            if (asset is Sprite ss) AddToLib(ss);
                }
                serializedObject.Update(); EditorUtility.SetDirty(_lib); e.Use();
            }
        }
    }

    private void AddToLib(Sprite s)
    {
        if (s == null) return;
        // 避免重复添加
        if (_lib.AllFrames.Any(f => f.Sprite == s)) return;

        SpriteFrameData frame = new SpriteFrameData();
        frame.Name = s.name;
        frame.Sprite = s;
        frame.Offset = Vector2.zero;
        frame.HurtBoxes = new List<LogicBox>();
        frame.HitBoxes = new List<LogicBox>();
        
        _lib.AllFrames.Add(frame);
    }
}
