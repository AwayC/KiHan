using KiHan.Config;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Main Camera 的 Inspector：Play 模式下在下方显示当前 3D 地图的相机参数 (MapConfig.camera3D)，
/// 直接改的是地图资源本身，即时生效，退出 Play 后保留
/// </summary>
[CustomEditor(typeof(CameraControllor))]
public class CameraControllorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("当前地图 3D 相机参数", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play 后显示当前地图的参数；不运行时请直接选中 Maps/<id>/<id>.asset 编辑", MessageType.Info);
            return;
        }

        MapConfig map = MapManager.Instance != null ? MapManager.Instance.CurrentMap : null;
        if (map == null || !map.is3D)
        {
            EditorGUILayout.HelpBox("当前地图不是 3D 地图", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("地图资源", map, typeof(MapConfig), false);
        }

        var so = new SerializedObject(map);
        so.Update();
        SerializedProperty settings = so.FindProperty(nameof(MapConfig.camera3D));
        SerializedProperty end = settings.GetEndProperty();
        settings.NextVisible(true);
        while (!SerializedProperty.EqualContents(settings, end))
        {
            EditorGUILayout.PropertyField(settings, true);
            if (!settings.NextVisible(false)) break;
        }
        so.ApplyModifiedProperties();
    }
}
