using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationFrameData))]
public class AnimationFrameDataEditor : Editor
{
    private AnimationFrameData _data;
    private double _lastUpdateTime;
    private int _previewIndex;
    private bool _isPaused = false;

    private void OnEnable()
    {
        _data = (AnimationFrameData)target;
        _lastUpdateTime = EditorApplication.timeSinceStartup;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (_data.Frames == null || _data.Frames.Count == 0) return;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("动作预览 (支持 Duration 停留帧)", EditorStyles.boldLabel);

        // --- 预览计时逻辑 ---
        float baseInterval = 1f / Mathf.Max(1f, _data.FrameRate);
        double currentTime = EditorApplication.timeSinceStartup;

        if (!_isPaused && Application.isPlaying == false)
        {
            int duration = _data.Frames[_previewIndex].Duration;
            if (currentTime - _lastUpdateTime > baseInterval * duration)
            {
                _previewIndex = (_previewIndex + 1) % _data.Frames.Count;
                _lastUpdateTime = currentTime;
                Repaint();
            }
        }

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
        }
    }

    private void DrawSpriteIndustrial(Rect container, Sprite sprite)
    {
        Texture2D tex = sprite.texture;
        Rect sRect = sprite.textureRect;
        float ratio = sRect.width / sRect.height;
        float finalW = container.height * ratio;
        float finalH = container.height;
        if (finalW > container.width) { finalW = container.width; finalH = finalW / ratio; }

        Rect drawRect = new Rect(container.x + (container.width - finalW) / 2, container.y + (container.height - finalH) / 2, finalW, finalH);
        Rect uv = new Rect(sRect.x / tex.width, sRect.y / tex.height, sRect.width / tex.width, sRect.height / tex.height);
        GUI.DrawTextureWithTexCoords(drawRect, tex, uv, true);
    }
}