using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class UIManager : UnitySingleton<UIManager>
    {
        private Dictionary<string, BasePanel> panelCache = new Dictionary<string, BasePanel>();
        private Transform currentCanvas;
        private int _currentSortOrder = 10; // 用于管理多 Canvas 的渲染层级

        protected override void Awake()
        {
            base.Awake();
        }

        private void BringToFront(BasePanel panel)
        {
            panel.transform.SetAsLastSibling();
            
            // 递归修正所有缩放为 0 的节点（处理预制体保存时的 Bug）
            FixZeroScales(panel.transform);

            // 获取所有 Canvas (包括子节点的嵌套 Canvas)
            Canvas[] canvases = panel.GetComponentsInChildren<Canvas>(true);
            
            int targetOrder;
            if (panel.SortingPriority > 0)
            {
                targetOrder = panel.SortingPriority;
            }
            else
            {
                _currentSortOrder++;
                targetOrder = _currentSortOrder;
            }

            foreach (var c in canvases)
            {
                c.overrideSorting = true;
                c.sortingOrder = targetOrder;
            }

            Debug.Log($"[UIManager] BringToFront: {panel.gameObject.name}, Type: {panel.GetType().Name}, SortingOrder: {targetOrder}, CanvasCount: {canvases.Length}");
        }

        private void FixZeroScales(Transform t)
        {
            if (t.localScale.x == 0 || t.localScale.y == 0 || t.localScale.z == 0)
            {
                t.localScale = Vector3.one;
            }
            foreach (Transform child in t)
            {
                FixZeroScales(child);
            }
        }

        public T OpenPanel<T>(string panelPath, object data = null) where T : BasePanel
        {
            Debug.Log($"[UIManager] Request to OpenPanel: {panelPath}");
            if (panelCache.TryGetValue(panelPath, out BasePanel cachedPanel))
            {
                if (cachedPanel != null)
                {
                    Debug.Log($"[UIManager] Found {panelPath} in cache. Calling OnOpen.");
                    cachedPanel.OnOpen(data);
                    BringToFront(cachedPanel);
                    return cachedPanel as T;
                }
                else
                {
                    Debug.LogWarning($"[UIManager] Cache entry for {panelPath} was null, removing.");
                    panelCache.Remove(panelPath);
                }
            }

            Debug.Log($"[UIManager] Loading prefab for {panelPath}");
            GameObject prefab = Resources.Load<GameObject>(panelPath);
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] 未找到 UI Prefab，路径: {panelPath}");
                return null;
            }

            GameObject go;
            // 核心修复：如果预制体本身或其子节点包含 Canvas（例如你直接把 Canvas 做成了子节点或 Prefab）
            // 这种通常是独立的 UI 系统，直接生成在根目录，避免嵌套导致的问题
            if (prefab.GetComponentInChildren<Canvas>() != null)
            {
                go = Instantiate(prefab); // 直接生成在根目录
                go.transform.localScale = Vector3.one; // 强制缩放为 1，防止预制体默认 0 导致看不见
                go.transform.localPosition = Vector3.zero;
                
                // 如果场景原本没有 Canvas，就把这个当做主 Canvas
                if (currentCanvas == null || currentCanvas.name == "UICanvas") 
                {
                    currentCanvas = go.transform;
                }
                
                // 同样检查 EventSystem
                if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    GameObject eventSystemGo = new GameObject("EventSystem");
                    eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
            }
            else
            {
                // 普通面板，放到 currentCanvas 下面
                if (currentCanvas == null)
                {
                    Canvas canvas = GameObject.FindObjectOfType<Canvas>();
                    if (canvas == null)
                    {
                        GameObject canvasGo = new GameObject("UICanvas");
                        canvas = canvasGo.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                        
                        if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                        {
                            GameObject eventSystemGo = new GameObject("EventSystem");
                            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                        }
                    }
                    currentCanvas = canvas.transform;
                }

                go = Instantiate(prefab, currentCanvas);
                go.transform.localScale = Vector3.one; // 强制缩放为 1
                go.transform.localPosition = Vector3.zero;
                
                // 重置 RectTransform 保证大小正常
                RectTransform rect = go.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // 如果需要全屏拉伸
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }

            T panel = go.GetComponent<T>();
            if (panel == null)
            {
                panel = go.AddComponent<T>();
            }

            panelCache.Add(panelPath, panel);
            BringToFront(panel);
            panel.OnOpen(data);
            return panel;
        }

        public void ClosePanel(string panelPath)
        {
            if (panelCache.TryGetValue(panelPath, out BasePanel panel))
            {
                if (panel != null) panel.OnClose();
            }
        }

        public void ClearCacheOnSceneChange()
        {
            panelCache.Clear();
            currentCanvas = null;
        }

        public void ShowTip(string message)
        {
            OpenPanel<KiHan.View.UI.System.SystemTipPanel>(UIConst.SystemTipPanel, message);
        }
    }
}