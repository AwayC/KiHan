using System.Collections.Generic;
using KiHan.Asset;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KiHan.UI
{
    /// <summary>
    /// UI 管理器：分层根节点 (UIRoot)、面板加载 / 缓存、Normal 层返回栈、Popup 层模态遮罩
    /// - 面板按预制体地址打开 (C# 用 UIPanels 常量，Lua 用 UI.Open)；预制体根节点挂 LuaPanel，层级与缓存在组件上
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
        private readonly Dictionary<string, LuaPanel> _panels = new Dictionary<string, LuaPanel>();
        private readonly List<LuaPanel> _normalStack = new List<LuaPanel>();
        private readonly List<LuaPanel> _popups = new List<LuaPanel>();
        private readonly List<LuaPanel> _scratch = new List<LuaPanel>();
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

        /// <summary>
        /// 按预制体地址打开面板 (未创建时加载并实例化)
        /// </summary>
        public LuaPanel Open(string address, object data = null)
        {
            if (string.IsNullOrEmpty(address)) return null;

            if (!_panels.TryGetValue(address, out LuaPanel panel) || panel == null)
            {
                panel = Create(address);
                if (panel == null) return null;
                _panels[address] = panel;
            }

            panel.transform.SetAsLastSibling();

            if (panel.Layer == UILayer.Normal)
            {
                _normalStack.Remove(panel);
                if (_normalStack.Count > 0) _normalStack[_normalStack.Count - 1].gameObject.SetActive(false);
                _normalStack.Add(panel);
            }
            else if (panel.Layer == UILayer.Popup)
            {
                _popups.Remove(panel);
                _popups.Add(panel);
            }

            panel.Open(data);
            RefreshPopupMask();
            return panel;
        }

        public void Close(string address)
        {
            if (address != null && _panels.TryGetValue(address, out LuaPanel panel)) Close(panel);
        }

        public void Close(LuaPanel panel)
        {
            if (panel == null || !panel.IsOpen) return;

            bool wasNormalTop = panel.Layer == UILayer.Normal && _normalStack.Count > 0 && _normalStack[_normalStack.Count - 1] == panel;
            _normalStack.Remove(panel);
            _popups.Remove(panel);

            panel.Close(() =>
            {
                if (panel == null || panel.IsOpen) return; // 隐藏动画期间被重新打开
                if (panel.Cache)
                {
                    panel.gameObject.SetActive(false);
                }
                else
                {
                    _panels.Remove(panel.Address);
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
            foreach (LuaPanel panel in _panels.Values)
            {
                if (panel != null && panel.IsOpen && panel.Layer == layer) _scratch.Add(panel);
            }
            foreach (LuaPanel panel in _scratch) Close(panel);
            _scratch.Clear();
        }

        /// <summary>
        /// 已创建的面板 (未创建或已销毁时为 null)
        /// </summary>
        public LuaPanel Get(string address) => address != null && _panels.TryGetValue(address, out LuaPanel panel) && panel != null ? panel : null;

        public bool IsOpen(string address)
        {
            LuaPanel panel = Get(address);
            return panel != null && panel.IsOpen;
        }

        /// <summary>
        /// 飘字提示 (排队依次显示；与 Lua UI.Toast 相同)
        /// </summary>
        public void Toast(string message)
        {
            LuaPanel toast = IsOpen(UIPanels.Toast) ? Get(UIPanels.Toast) : Open(UIPanels.Toast);
            toast?.Call("Enqueue", message);
        }

        /// <summary>
        /// 销毁全部面板实例 (切换场景 / 重启流程时)
        /// </summary>
        public void ReleaseAll()
        {
            foreach (LuaPanel panel in _panels.Values)
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

        private LuaPanel Create(string address)
        {
            var prefab = AssetManager.Instance.Load<GameObject>(address);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] 找不到面板预制体 {address}");
                return null;
            }
            var proto = prefab.GetComponent<LuaPanel>();
            if (proto == null)
            {
                Debug.LogError($"[UIManager] 面板预制体 {address} 根节点缺少 LuaPanel");
                return null;
            }

            GameObject go = Instantiate(prefab, _layers[(int)proto.PanelLayer], false);
            go.name = prefab.name;
            var panel = go.GetComponent<LuaPanel>();
            panel.Address = address;
            panel.Create();
            return panel;
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

                // 镜像摆放 (绕 Y 轴转 180°) 的按钮也要能点：默认 true 会忽略背面朝向相机的图形 (如 HUD 的普攻键)
                go.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = false;

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
