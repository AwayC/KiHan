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
            Canvas c = panel.GetComponent<Canvas>();
            if (c != null)
            {
                c.overrideSorting = true;
                _currentSortOrder++;
                c.sortingOrder = _currentSortOrder;
                Debug.Log($"[UIManager] BringToFront: {panel.gameObject.name}, new sortingOrder: {_currentSortOrder}");
            }
            else
            {
                Debug.Log($"[UIManager] BringToFront: {panel.gameObject.name} (No Canvas component found, relies on hierarchy order)");
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
            // 核心修复：如果预制体本身自带 Canvas（例如你直接把 Canvas 做成了 Prefab）
            // 就不能再把它放到 currentCanvas 下面，否则会变成 Canvas 嵌套导致不可见
            if (prefab.GetComponent<Canvas>() != null)
            {
                go = Instantiate(prefab); // 直接生成在根目录
                
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
                
                // 重置 RectTransform 保证大小正常
                RectTransform rect = go.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    rect.localPosition = Vector3.zero;
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
    }
}