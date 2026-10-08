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

    private int _editMode = 0; // 0: None, 1: HurtBox, 2: HitBox, 3: Move Sprite, 4: Pan View, 5: Move Anchor
    private Vector2 _dragStartPos;
    private int _activeBoxIndex = -1;
    private int _activePresetIndex = 0;
    private int _activeAnchorIndex = -1;

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
            EditorGUILayout.LabelField("厚度预设与全局套用 (Side Presets)", EditorStyles.boldLabel);
            for (int i = 0; i < _presetsProp.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(_presetsProp.GetArrayElementAtIndex(i), GUIContent.none);
                    float val = _presetsProp.GetArrayElementAtIndex(i).floatValue;
                    if (GUILayout.Button("全局套用", GUILayout.Width(70))) ApplySideToAll(val);
                    if (GUILayout.Button(i == _activePresetIndex ? "★当前默认" : "设为默认", GUILayout.Width(70))) _activePresetIndex = i;
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        _presetsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }
            if (GUILayout.Button("+ 添加新厚度预设")) _presetsProp.InsertArrayElementAtIndex(_presetsProp.arraySize);
        }

        if (_framesProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("当前帧库为空，请拖拽切片图片到上方导入区。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        _currentIndex = Mathf.Clamp(_currentIndex, 0, _framesProp.arraySize - 1);

        EditorGUILayout.Space(5);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showOnionSkin = EditorGUILayout.Toggle("洋葱皮 (前一帧残影)", _showOnionSkin);
                _previewAlpha = EditorGUILayout.Slider("立绘透明度", _previewAlpha, 0.1f, 1.0f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _viewZoom = EditorGUILayout.Slider("画布缩放", _viewZoom, 0.1f, 10f);
                if (GUILayout.Button("复位视角", GUILayout.Width(70)))
                {
                    _viewOffset = Vector2.zero;
                    _viewZoom = 2.0f;
                }
            }
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
            EditorGUILayout.LabelField("帧列表快速检索与选择", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20))) _searchString = "";
            }
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(110));
            for (int i = 0; i < _framesProp.arraySize; i++)
            {
                string fName = _framesProp.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;
                if (!string.IsNullOrEmpty(_searchString) && !fName.ToLower().Contains(_searchString.ToLower())) continue;

                GUI.color = (i == _currentIndex) ? Color.cyan : Color.white;
                if (GUILayout.Button($"[{i:D3}] {fName}", EditorStyles.miniButtonLeft)) _currentIndex = i;
            }
            GUI.color = Color.white;
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawPreviewArea()
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            Rect rect = GUILayoutUtility.GetRect(0, 420, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            // 操作提示抬头
            Rect tipsRect = new Rect(rect.x + 8, rect.y + 6, rect.width - 16, 20);
            GUI.color = Color.yellow;
            GUI.Label(tipsRect, "[Shift+左键]拖拽画受击盒(蓝) | [Shift+右键]拖拽画攻击盒(红) | [Ctrl+左键]调立绘Offset | [Alt+左键]调锚点 | [滚轮]缩放 | [中键/右键]平移", EditorStyles.miniLabel);
            GUI.color = Color.white;

            // 关键修复：GUI.BeginGroup 内部坐标以 (0,0) 开始
            GUI.BeginGroup(rect);
            Vector2 groupCenter = new Vector2(rect.width * 0.5f, rect.height * 0.5f) + _viewOffset;

            // 绘制中心基准轴 (XY轴交叉十字线)
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawLine(new Vector3(0, groupCenter.y), new Vector3(rect.width, groupCenter.y));
            Handles.DrawLine(new Vector3(groupCenter.x, 0), new Vector3(groupCenter.x, rect.height));

            var frame = _lib.AllFrames[_currentIndex];

            // 1. 洋葱皮 (前一帧半透明残影)
            if (_showOnionSkin && _currentIndex > 0)
            {
                var prevFrame = _lib.AllFrames[_currentIndex - 1];
                DrawOneSprite(groupCenter, prevFrame.Sprite, prevFrame.Offset, _viewZoom, 0.25f, Color.white);
            }

            if (frame.Sprite != null)
            {
                // 2. 渲染本体 Sprite
                DrawOneSprite(groupCenter, frame.Sprite, frame.Offset, _viewZoom, _previewAlpha, Color.white);

                // 3. 渲染额外特效图层
                if (frame.ExtraLayers != null)
                {
                    foreach (var layer in frame.ExtraLayers)
                    {
                        if (layer.Sprite == null) continue;
                        Color drawColor = layer.TintColor;
                        if (drawColor.a == 0 && drawColor.r == 0 && drawColor.g == 0 && drawColor.b == 0) drawColor = Color.white;
                        DrawOneSprite(groupCenter, layer.Sprite, frame.Offset + layer.Offset, _viewZoom, _previewAlpha * drawColor.a, drawColor);
                    }
                }

                // 4. 绘制受击盒 (蓝色) 与 攻击盒 (红色)
                if (frame.HurtBoxes != null)
                {
                    foreach (var b in frame.HurtBoxes) DrawLogicBox(groupCenter, b, new Color(0f, 0.5f, 1f, 0.35f), Color.cyan, _viewZoom);
                }
                if (frame.HitBoxes != null)
                {
                    foreach (var b in frame.HitBoxes) DrawLogicBox(groupCenter, b, new Color(1f, 0f, 0f, 0.4f), Color.red, _viewZoom);
                }

                // 5. 绘制特效锚点
                if (frame.EffectAnchors != null)
                {
                    for (int i = 0; i < frame.EffectAnchors.Count; i++)
                    {
                        Vector2 aPos = groupCenter + new Vector2(frame.EffectAnchors[i].Position.x, -frame.EffectAnchors[i].Position.y) * _viewZoom;
                        Handles.color = (i == _activeAnchorIndex) ? Color.green : Color.yellow;
                        Handles.DrawSolidDisc(aPos, Vector3.forward, 5f);
                        GUI.color = Handles.color;
                        GUI.Label(new Rect(aPos.x + 8, aPos.y - 8, 120, 20), frame.EffectAnchors[i].Name, EditorStyles.miniBoldLabel);
                        GUI.color = Color.white;
                    }
                }
            }

            GUI.EndGroup();

            // 关键修复：窗口全局坐标中心 = rect.min + groupCenter
            HandleInput(rect, rect.min + groupCenter, _viewZoom);
        }
    }

    private void DrawOneSprite(Vector2 center, Sprite s, Vector2 off, float z, float a, Color t)
    {
        if (s == null || s.texture == null) return;
        Rect sr = s.textureRect;
        float w = sr.width * z;
        float h = sr.height * z;
        Rect dr = new Rect(center.x + off.x * z - w / 2, center.y - off.y * z - h / 2, w, h);
        Rect uv = new Rect(sr.x / s.texture.width, sr.y / s.texture.height, sr.width / s.texture.width, sr.height / s.texture.height);

        Color old = GUI.color;
        GUI.color = new Color(t.r, t.g, t.b, a);
        GUI.DrawTextureWithTexCoords(dr, s.texture, uv, true);
        GUI.color = old;
    }

    private void DrawLogicBox(Vector2 center, LogicBox b, Color fill, Color outline, float z)
    {
        if (b.Size.x <= 0 || b.Size.y <= 0) return;
        Rect r = new Rect(
            center.x + (b.Center.x - b.Size.x * 0.5f) * z,
            center.y - (b.Center.y + b.Size.y * 0.5f) * z,
            b.Size.x * z,
            b.Size.y * z
        );
        EditorGUI.DrawRect(r, fill);
        Handles.color = outline;
        Handles.DrawSolidRectangleWithOutline(r, Color.clear, outline);
    }

    private void HandleInput(Rect screenRect, Vector2 windowCenter, float zoom)
    {
        Event e = Event.current;
        if (_currentIndex < 0 || _currentIndex >= _lib.AllFrames.Count) return;
        var frame = _lib.AllFrames[_currentIndex];

        // 滚轮缩放画布
        if (screenRect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
        {
            _viewZoom = Mathf.Clamp(_viewZoom - e.delta.y * 0.08f, 0.2f, 10f);
            e.Use();
            Repaint();
            return;
        }

        // 转换鼠标坐标到相对于角色中心对齐的逻辑像素坐标 (Y向上为正)
        Vector2 mouseLogicPos = (e.mousePosition - windowCenter) / zoom;
        mouseLogicPos.y = -mouseLogicPos.y;

        if (e.type == EventType.MouseDown && screenRect.Contains(e.mousePosition))
        {
            if (e.shift)
            {
                // Shift + 鼠标绘制碰撞盒 (左键 HurtBox 蓝框, 右键 HitBox 红框)
                _editMode = (e.button == 0) ? 1 : 2;
                Undo.RecordObject(_lib, "Draw Box");
                _dragStartPos = mouseLogicPos;
                float side = (_lib.SidePresets.Count > _activePresetIndex) ? _lib.SidePresets[_activePresetIndex] : 80.0f;
                var nb = new LogicBox(mouseLogicPos, new Vector2(4f, 4f), side);

                if (_editMode == 1)
                {
                    frame.HurtBoxes.Add(nb);
                    _activeBoxIndex = frame.HurtBoxes.Count - 1;
                }
                else
                {
                    frame.HitBoxes.Add(nb);
                    _activeBoxIndex = frame.HitBoxes.Count - 1;
                }
                EditorUtility.SetDirty(_lib);
            }
            else if (e.control && e.button == 0)
            {
                // Ctrl + 左键拖动立绘 Offset
                _editMode = 3;
                Undo.RecordObject(_lib, "Move Sprite Offset");
            }
            else if (e.alt && e.button == 0)
            {
                // Alt + 左键吸附拖拽特效锚点
                float minDist = float.MaxValue;
                _activeAnchorIndex = -1;
                if (frame.EffectAnchors != null)
                {
                    for (int i = 0; i < frame.EffectAnchors.Count; i++)
                    {
                        float dist = Vector2.Distance(frame.EffectAnchors[i].Position, mouseLogicPos);
                        if (dist < minDist && dist < (25f / zoom))
                        {
                            minDist = dist;
                            _activeAnchorIndex = i;
                        }
                    }
                }
                if (_activeAnchorIndex != -1)
                {
                    _editMode = 5;
                    Undo.RecordObject(_lib, "Move Effect Anchor");
                }
            }
            else if (e.button == 1 || e.button == 2)
            {
                // 中键或右键拖动画布 (Pan)
                _editMode = 4;
            }
            else
            {
                _editMode = 0;
            }

            if (_editMode != 0)
            {
                _dragStartPos = (_editMode == 4) ? e.mousePosition : mouseLogicPos;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && _editMode != 0)
        {
            if (_editMode == 4)
            {
                _viewOffset += (e.mousePosition - _dragStartPos);
                _dragStartPos = e.mousePosition;
            }
            else if (_editMode == 3)
            {
                frame.Offset = mouseLogicPos;
                EditorUtility.SetDirty(_lib);
            }
            else if (_editMode == 5 && _activeAnchorIndex >= 0 && _activeAnchorIndex < frame.EffectAnchors.Count)
            {
                frame.EffectAnchors[_activeAnchorIndex].Position = mouseLogicPos;
                EditorUtility.SetDirty(_lib);
            }
            else if (_editMode == 1 || _editMode == 2)
            {
                Vector2 min = Vector2.Min(_dragStartPos, mouseLogicPos);
                Vector2 max = Vector2.Max(_dragStartPos, mouseLogicPos);
                Vector2 center = (min + max) * 0.5f;
                Vector2 size = new Vector2(Mathf.Max(4f, max.x - min.x), Mathf.Max(4f, max.y - min.y));

                if (_editMode == 1 && _activeBoxIndex >= 0 && _activeBoxIndex < frame.HurtBoxes.Count)
                {
                    var b = frame.HurtBoxes[_activeBoxIndex];
                    b.Center = center;
                    b.Size = size;
                    frame.HurtBoxes[_activeBoxIndex] = b;
                }
                else if (_editMode == 2 && _activeBoxIndex >= 0 && _activeBoxIndex < frame.HitBoxes.Count)
                {
                    var b = frame.HitBoxes[_activeBoxIndex];
                    b.Center = center;
                    b.Size = size;
                    frame.HitBoxes[_activeBoxIndex] = b;
                }
                EditorUtility.SetDirty(_lib);
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && _editMode != 0)
        {
            _editMode = 0;
            EditorUtility.SetDirty(_lib);
            e.Use();
        }
    }

    private void DrawFrameDetails()
    {
        if (_currentIndex < 0 || _currentIndex >= _framesProp.arraySize) return;
        var p = _framesProp.GetArrayElementAtIndex(_currentIndex);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField($"当前帧属性详情 [{_currentIndex:D3}] : {p.FindPropertyRelative("Name").stringValue}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Sprite"), new GUIContent("切片图片素材"));
            EditorGUILayout.PropertyField(p.FindPropertyRelative("Offset"), new GUIContent("脚底中心对齐偏移"));

            DrawAnchorList(p.FindPropertyRelative("EffectAnchors"), "特效挂点 (Effect Anchors)");
            DrawCustomBoxList(p.FindPropertyRelative("HurtBoxes"), "受击盒 (HurtBoxes - 蓝)", isHitBox: false);
            DrawCustomBoxList(p.FindPropertyRelative("HitBoxes"), "攻击盒 (HitBoxes - 红)", isHitBox: true);
            DrawEffectLayerList(p.FindPropertyRelative("ExtraLayers"));
        }
    }

    private void DrawAnchorList(SerializedProperty listProp, string label)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("+ 新增挂点", GUILayout.Width(80)))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newAnchor = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newAnchor.FindPropertyRelative("Name").stringValue = "Anchor" + listProp.arraySize;
                newAnchor.FindPropertyRelative("Position").vector2Value = Vector2.zero;
                EditorUtility.SetDirty(_lib);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty anchorProp = listProp.GetArrayElementAtIndex(i);
                    GUI.color = (_activeAnchorIndex == i) ? Color.green : Color.white;
                    if (GUILayout.Button("●", EditorStyles.miniButton, GUILayout.Width(22))) _activeAnchorIndex = i;
                    GUI.color = Color.white;

                    EditorGUILayout.PropertyField(anchorProp.FindPropertyRelative("Name"), GUIContent.none, GUILayout.Width(90));
                    EditorGUILayout.PropertyField(anchorProp.FindPropertyRelative("Position"), GUIContent.none);
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                        EditorUtility.SetDirty(_lib);
                        break;
                    }
                }
            }
        }
    }

    private void DrawEffectLayerList(SerializedProperty listProp)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("同步附带特效层 (刀光/气劲)", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("+ 新增图层", GUILayout.Width(80)))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newLayer = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newLayer.FindPropertyRelative("TintColor").colorValue = Color.white;
                newLayer.FindPropertyRelative("OrderOffset").intValue = 1;
                newLayer.FindPropertyRelative("Offset").vector2Value = Vector2.zero;
                EditorUtility.SetDirty(_lib);
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
                        if (GUILayout.Button("×", GUILayout.Width(20)))
                        {
                            listProp.DeleteArrayElementAtIndex(i);
                            EditorUtility.SetDirty(_lib);
                            break;
                        }
                    }
                    EditorGUILayout.PropertyField(layer.FindPropertyRelative("Offset"), new GUIContent("相对偏移"));
                    EditorGUILayout.PropertyField(layer.FindPropertyRelative("TintColor"), new GUIContent("颜色叠加"));
                    EditorGUILayout.PropertyField(layer.FindPropertyRelative("OrderOffset"), new GUIContent("深度层级偏移"));
                }
            }
        }
    }

    private void DrawCustomBoxList(SerializedProperty listProp, string label, bool isHitBox)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                if (GUILayout.Button("+ 添加盒", GUILayout.Width(65)))
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                    var newBox = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                    float side = (_lib.SidePresets.Count > _activePresetIndex) ? _lib.SidePresets[_activePresetIndex] : 80.0f;
                    newBox.FindPropertyRelative("Center").vector2Value = isHitBox ? new Vector2(30, 50) : new Vector2(0, 50);
                    newBox.FindPropertyRelative("Size").vector2Value = isHitBox ? new Vector2(50, 50) : new Vector2(50, 120);
                    newBox.FindPropertyRelative("Side").floatValue = side;
                    EditorUtility.SetDirty(_lib);
                }
            }

            for (int i = 0; i < listProp.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty boxProp = listProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(20));
                    EditorGUILayout.LabelField("中心:", GUILayout.Width(30));
                    EditorGUILayout.PropertyField(boxProp.FindPropertyRelative("Center"), GUIContent.none, GUILayout.Width(90));
                    EditorGUILayout.LabelField("尺寸:", GUILayout.Width(30));
                    EditorGUILayout.PropertyField(boxProp.FindPropertyRelative("Size"), GUIContent.none, GUILayout.Width(90));

                    float currentSide = boxProp.FindPropertyRelative("Side").floatValue;
                    if (EditorGUILayout.DropdownButton(new GUIContent($"厚:{currentSide:F0}"), FocusType.Passive, GUILayout.Width(55)))
                    {
                        GenericMenu menu = new GenericMenu();
                        foreach (float preset in _lib.SidePresets)
                        {
                            float val = preset;
                            menu.AddItem(new GUIContent(val.ToString("F0")), Mathf.Approximately(val, currentSide), () =>
                            {
                                boxProp.FindPropertyRelative("Side").floatValue = val;
                                serializedObject.ApplyModifiedProperties();
                                EditorUtility.SetDirty(_lib);
                            });
                        }
                        menu.ShowAsContext();
                    }

                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                        EditorUtility.SetDirty(_lib);
                        break;
                    }
                }
            }
        }
    }

    private void DrawNavigator()
    {
        using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
        {
            if (GUILayout.Button("<< 上一帧", GUILayout.Height(24)))
            {
                _currentIndex = Mathf.Max(0, _currentIndex - 1);
            }

            EditorGUILayout.LabelField($"{_currentIndex + 1} / {_framesProp.arraySize}", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(24));

            if (GUILayout.Button("下一帧 >>", GUILayout.Height(24)))
            {
                _currentIndex = Mathf.Min(_framesProp.arraySize - 1, _currentIndex + 1);
            }

            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("删除当前帧", GUILayout.Width(90), GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("删除帧", $"确定删除当前切片帧 [{_currentIndex}] 吗？", "确定删除", "取消"))
                {
                    _framesProp.DeleteArrayElementAtIndex(_currentIndex);
                    _currentIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, _framesProp.arraySize - 1));
                    EditorUtility.SetDirty(_lib);
                }
            }
            GUI.color = Color.white;
        }
    }

    private void ApplySideToAll(float side)
    {
        if (!EditorUtility.DisplayDialog("批量更新厚度", $"确定将帧库内所有攻击盒与受击盒的纵深厚度统一设为 {side:F1} 吗？", "确定", "取消")) return;
        Undo.RecordObject(_lib, "Global Apply Side");
        foreach (var f in _lib.AllFrames)
        {
            for (int j = 0; j < f.HurtBoxes.Count; j++)
            {
                var b = f.HurtBoxes[j];
                b.Side = side;
                f.HurtBoxes[j] = b;
            }
            for (int j = 0; j < f.HitBoxes.Count; j++)
            {
                var b = f.HitBoxes[j];
                b.Side = side;
                f.HitBoxes[j] = b;
            }
        }
        EditorUtility.SetDirty(_lib);
    }

    private void DrawDropZone()
    {
        Event e = Event.current;
        Rect dropRect = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "拖拽 Sprite 切片图片或文件夹到此区域，批量导入新帧", EditorStyles.helpBox);

        if (dropRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(_lib, "Batch Import Sprites");
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is Sprite s)
                    {
                        AddToLib(s);
                    }
                    else if (obj is Texture2D t)
                    {
                        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)))
                        {
                            if (asset is Sprite ss) AddToLib(ss);
                        }
                    }
                }
                serializedObject.Update();
                EditorUtility.SetDirty(_lib);
                e.Use();
            }
        }
    }

    private void AddToLib(Sprite s)
    {
        if (s == null) return;
        if (_lib.AllFrames.Any(f => f.Sprite == s)) return;

        SpriteFrameData frame = new SpriteFrameData
        {
            Name = s.name,
            Sprite = s,
            Offset = Vector2.zero,
            HurtBoxes = new List<LogicBox>(),
            HitBoxes = new List<LogicBox>()
        };

        _lib.AllFrames.Add(frame);
    }
}
