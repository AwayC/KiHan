using KiHan.View.Effects;
using KiHan.View.Render;
using UnityEditor;
using UnityEngine;

namespace KiHan.Editor
{
    /// <summary>
    /// 编辑模式下预览特效 prefab，不进入 Play 模式 (不启动游戏)：
    /// 在当前场景原点生成一个不保存的临时实例，按时间驱动 Fx* 动画与粒子 (FxAnimatedBase.SampleTree)，在 Scene 窗口观看。
    /// 关闭窗口、换 prefab、进入 Play 模式时删除临时实例
    /// </summary>
    public class EffectPreviewWindow : EditorWindow
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private GameObject _instance;
        private float _duration = 1f;
        private float _time;
        private bool _playing;
        private bool _loop = true;
        private double _lastTick;

        [MenuItem("KiHan/资源/特效预览 (编辑模式)")]
        private static void Open()
        {
            var window = GetWindow<EffectPreviewWindow>("特效预览");
            if (Selection.activeObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)) window.SetPrefab(go);
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            DestroyInstance();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go) && go != _prefab)
            {
                SetPrefab(go);
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Play 模式下不可用。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("在 Project 窗口选中特效 prefab 即可预览；临时实例生成在当前场景原点，不会保存进场景。", MessageType.None);
            var picked = (GameObject)EditorGUILayout.ObjectField("特效 prefab", _prefab, typeof(GameObject), false);
            if (picked != _prefab) SetPrefab(picked);
            if (_instance == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "暂停" : "播放", GUILayout.Height(26)))
                {
                    if (!_playing && _time >= _duration) _time = 0f;
                    _playing = !_playing;
                    _lastTick = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("重播", GUILayout.Height(26)))
                {
                    _time = 0f;
                    _playing = true;
                    _lastTick = EditorApplication.timeSinceStartup;
                    Sample();
                }
                _loop = GUILayout.Toggle(_loop, "循环", GUILayout.Width(50));
            }

            _duration = Mathf.Max(0.05f, EditorGUILayout.FloatField("时长 (秒)", _duration));
            float time = EditorGUILayout.Slider("时间", _time, 0f, _duration);
            if (!Mathf.Approximately(time, _time))
            {
                _time = time;
                _playing = false;
                Sample();
            }
            if (GUILayout.Button("在 Scene 窗口中定位")) FocusInstance();
        }

        private void SetPrefab(GameObject prefab)
        {
            DestroyInstance();
            _prefab = prefab;
            _time = 0f;
            _playing = false;
            if (_prefab == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            _instance = (GameObject)PrefabUtility.InstantiatePrefab(_prefab);
            _instance.name = $"[特效预览] {_prefab.name}";
            _instance.hideFlags = HideFlags.DontSave;
            foreach (Transform child in _instance.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.DontSave;
            _instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var view = _instance.GetComponent<EffectView>();
            float duration = view != null ? new SerializedObject(view).FindProperty("fallbackDuration").floatValue : 0f;
            _duration = duration > 0f ? duration : 1f;

            Sample();
            _playing = true;
            _lastTick = EditorApplication.timeSinceStartup;
            FocusInstance();
        }

        private void Tick()
        {
            if (!_playing || _instance == null) return;
            double now = EditorApplication.timeSinceStartup;
            _time += (float)(now - _lastTick);
            _lastTick = now;
            if (_time >= _duration)
            {
                if (_loop) _time = 0f;
                else
                {
                    _time = _duration;
                    _playing = false;
                }
            }
            Sample();
            Repaint();
        }

        private void Sample()
        {
            if (_instance == null) return;
            FxAnimatedBase.SampleTree(_instance, _time);
            SceneView.RepaintAll();
        }

        private void FocusInstance()
        {
            if (_instance == null || SceneView.lastActiveSceneView == null) return;
            SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 1f, 0f), Quaternion.identity, 4f, true);
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) DestroyInstance();
        }

        private void DestroyInstance()
        {
            _playing = false;
            if (_instance != null) DestroyImmediate(_instance);
            _instance = null;
        }
    }
}
