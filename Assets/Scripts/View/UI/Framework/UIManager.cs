using System;
using System.Collections.Generic;
using System.Reflection;
using KiHan.Asset;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// UI 管理器：分层根节点 (UIRoot)、面板加载 / 缓存、Normal 层返回栈、Popup 层模态遮罩、Toast / 确认框快捷入口
    /// - 面板类必须带 UIPanelAttribute (地址与层级)，预制体根节点挂该面板脚本 (缺失时报错，不在运行时补挂)
    /// - Normal 层是栈：打开新界面时隐藏下面的界面，关闭栈顶时恢复下一个
    /// - Popup 层：最上层弹窗下方放一张半透明遮罩挡住点击
    /// </summary>
    public sealed class UIManager : UnitySingleton<UIManager>
    {
        /// <summary>
        /// 参考分辨率：Device Simulator 华为 P40 Pro 横屏 (现有界面均按它摆放)，按高度适配
        /// </summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(2640f, 1200f);

        private const int LayerCount = 7;
        private const int SortingStep = 100;
        private static readonly Color MaskColor = new Color(0f, 0f, 0f, 0.6f);

        private readonly RectTransform[] _layers = new RectTransform[LayerCount];
        private readonly Dictionary<Type, UIPanel> _panels = new Dictionary<Type, UIPanel>();
        private readonly Dictionary<Type, UIPanelAttribute> _meta = new Dictionary<Type, UIPanelAttribute>();
        private readonly List<UIPanel> _normalStack = new List<UIPanel>();
        private readonly List<UIPanel> _popups = new List<UIPanel>();
        private readonly List<UIPanel> _scratch = new List<UIPanel>();
        private RectTransform _popupMask;
        private bool _built;

        protected override void Awake()
        {
            base.Awake();
            Build();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
        }

        #region 打开 / 关闭

        public T Open<T>() where T : UIPanel => (T)Open(typeof(T), null);

        public T Open<T, TData>(TData data) where T : UIPanel<TData> => (T)Open(typeof(T), data);

        public void Close<T>() where T : UIPanel
        {
            if (_panels.TryGetValue(typeof(T), out UIPanel panel)) Close(panel);
        }

        public void Close(UIPanel panel)
        {
            if (panel == null || !panel.IsOpen) return;

            bool wasNormalTop = panel.Layer == UILayer.Normal && _normalStack.Count > 0 && _normalStack[_normalStack.Count - 1] == panel;
            _normalStack.Remove(panel);
            _popups.Remove(panel);

            Type type = panel.GetType();
            bool cache = Meta(type).Cache;
            panel.Close(() =>
            {
                if (panel == null || panel.IsOpen) return; // 隐藏动画期间被重新打开
                if (cache)
                {
                    panel.gameObject.SetActive(false);
                }
                else
                {
                    _panels.Remove(type);
                    panel.Release();
                    Destroy(panel.gameObject);
                }
            });

            // 恢复 Normal 栈中的下一个界面
            if (wasNormalTop && _normalStack.Count > 0) _normalStack[_normalStack.Count - 1].gameObject.SetActive(true);
            RefreshPopupMask();
        }

        /// <summary>
        /// 返回：关闭最上层弹窗；没有弹窗时关闭 Normal 栈顶 (栈底界面不关闭)。返回是否处理
        /// </summary>
        public bool Back()
        {
            if (_popups.Count > 0)
            {
                Close(_popups[_popups.Count - 1]);
                return true;
            }
            if (_normalStack.Count > 1)
            {
                Close(_normalStack[_normalStack.Count - 1]);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 关闭某一层的全部面板 (流程切换时调用)
        /// </summary>
        public void CloseLayer(UILayer layer)
        {
            _scratch.Clear();
            foreach (UIPanel panel in _panels.Values)
            {
                if (panel != null && panel.IsOpen && panel.Layer == layer) _scratch.Add(panel);
            }
            foreach (UIPanel panel in _scratch) Close(panel);
            _scratch.Clear();
        }

        public T Get<T>() where T : UIPanel => _panels.TryGetValue(typeof(T), out UIPanel panel) ? (T)panel : null;

        public bool IsOpen<T>() where T : UIPanel => _panels.TryGetValue(typeof(T), out UIPanel panel) && panel != null && panel.IsOpen;

        /// <summary>
        /// 飘字提示 (排队依次显示)
        /// </summary>
        public void Toast(string message)
        {
            ToastPanel toast = IsOpen<ToastPanel>() ? Get<ToastPanel>() : Open<ToastPanel>();
            toast?.Enqueue(message);
        }

        /// <summary>
        /// 通用确认弹窗
        /// </summary>
        public void Confirm(string text, Action onConfirm, Action onCancel = null) =>
            Open<ConfirmPopup, ConfirmPopup.Args>(new ConfirmPopup.Args { Text = text, OnConfirm = onConfirm, OnCancel = onCancel });

        /// <summary>
        /// 销毁全部面板实例 (切换场景 / 重启流程时)
        /// </summary>
        public void ReleaseAll()
        {
            foreach (UIPanel panel in _panels.Values)
            {
                if (panel == null) continue;
                panel.Release();
                Destroy(panel.gameObject);
            }
            _panels.Clear();
            _normalStack.Clear();
            _popups.Clear();
            RefreshPopupMask();
        }

        private UIPanel Open(Type type, object data)
        {
            UIPanelAttribute meta = Meta(type);
            if (meta == null) return null;

            if (!_panels.TryGetValue(type, out UIPanel panel) || panel == null)
            {
                panel = Create(type, meta);
                if (panel == null) return null;
                _panels[type] = panel;
            }

            panel.transform.SetAsLastSibling();

            if (meta.Layer == UILayer.Normal)
            {
                _normalStack.Remove(panel);
                if (_normalStack.Count > 0) _normalStack[_normalStack.Count - 1].gameObject.SetActive(false);
                _normalStack.Add(panel);
            }
            else if (meta.Layer == UILayer.Popup)
            {
                _popups.Remove(panel);
                _popups.Add(panel);
            }

            panel.Open(data);
            RefreshPopupMask();
            return panel;
        }

        private UIPanel Create(Type type, UIPanelAttribute meta)
        {
            var prefab = AssetManager.Instance.Load<GameObject>(meta.Path);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] 找不到面板预制体 {meta.Path} ({type.Name})");
                return null;
            }

            GameObject go = Instantiate(prefab, _layers[(int)meta.Layer], false);
            go.name = prefab.name;
            var panel = go.GetComponent(type) as UIPanel;
            if (panel == null)
            {
                Debug.LogError($"[UIManager] 面板预制体 {meta.Path} 根节点缺少 {type.Name}");
                Destroy(go);
                return null;
            }
            panel.Create(meta.Layer);
            return panel;
        }

        private UIPanelAttribute Meta(Type type)
        {
            if (_meta.TryGetValue(type, out UIPanelAttribute meta)) return meta;
            meta = type.GetCustomAttribute<UIPanelAttribute>();
            if (meta == null) Debug.LogError($"[UIManager] 面板 {type.Name} 缺少 UIPanelAttribute");
            _meta[type] = meta;
            return meta;
        }

        #endregion

        #region 根节点

        /// <summary>
        /// 层级根节点 (面板父节点)
        /// </summary>
        public RectTransform LayerRoot(UILayer layer) => _layers[(int)layer];

        private void Build()
        {
            if (_built) return;
            _built = true;

            for (int i = 0; i < LayerCount; i++)
            {
                var go = new GameObject($"Layer_{(UILayer)i}", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.layer = LayerMask.NameToLayer("UI");
                go.transform.SetParent(transform, false);

                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = i * SortingStep;

                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                _layers[i] = (RectTransform)go.transform;
            }

            _popupMask = CreateMask(_layers[(int)UILayer.Popup]);

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
        }

        private static RectTransform CreateMask(RectTransform parent)
        {
            var go = new GameObject("ModalMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = MaskColor;
            go.SetActive(false);
            return rect;
        }

        /// <summary>
        /// 遮罩放在最上层弹窗的正下方，没有打开的弹窗时隐藏
        /// </summary>
        private void RefreshPopupMask()
        {
            if (_popupMask == null) return;
            if (_popups.Count == 0)
            {
                _popupMask.gameObject.SetActive(false);
                return;
            }
            _popupMask.gameObject.SetActive(true);
            _popupMask.SetAsLastSibling();
            _popups[_popups.Count - 1].transform.SetAsLastSibling();
        }

        #endregion
    }
}
