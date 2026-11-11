using System;
using KiHan.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KiHan.HotUpdate
{
    /// <summary>
    /// 热更进度覆盖层：纯代码搭建。热更阶段资源包尚不可用，这是 ui.md「面板必须是预制体 + @ 绑定」的唯一例外。
    /// 没有下载、也没有错误时不显示。
    /// </summary>
    public sealed class HotUpdateView : MonoBehaviour
    {
        private const float BarWidth = 1400f;
        private const float BarHeight = 24f;

        public event Action RetryClicked;

        private GameObject _panel;
        private Text _text;
        private GameObject _bar;
        private RectTransform _fill;
        private GameObject _retry;

        public static HotUpdateView Create()
        {
            var go = new GameObject("HotUpdateView");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<HotUpdateView>();
            view.Build();
            return view;
        }

        public void SetProgress(float ratio, string text)
        {
            Show(text, bar: true, retry: false);
            _fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        public void ShowRetry(string text) => Show(text, bar: false, retry: true);

        public void ShowFatal(string text) => Show(text, bar: false, retry: false);

        public void Close()
        {
            Destroy(gameObject);
        }

        private void Show(string text, bool bar, bool retry)
        {
            _panel.SetActive(true);
            _text.text = text;
            _bar.SetActive(bar);
            _retry.SetActive(retry);
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIManager.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform panel = CreateImage("Background", canvasGo.transform, Color.black);
            Stretch(panel);
            _panel = panel.gameObject;

            _text = CreateText("Text", panel, font, 40);
            _text.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            _text.rectTransform.sizeDelta = new Vector2(BarWidth, 80f);

            RectTransform bar = CreateImage("Bar", panel, new Color(1f, 1f, 1f, 0.2f));
            bar.sizeDelta = new Vector2(BarWidth, BarHeight);
            bar.anchoredPosition = new Vector2(0f, -20f);
            _bar = bar.gameObject;

            _fill = CreateImage("Fill", bar, new Color(1f, 0.6f, 0.1f, 1f));
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;

            RectTransform retry = CreateImage("Retry", panel, new Color(1f, 0.6f, 0.1f, 1f));
            retry.sizeDelta = new Vector2(360f, 100f);
            retry.anchoredPosition = new Vector2(0f, -80f);
            Text label = CreateText("Label", retry, font, 40);
            label.color = Color.black;
            label.text = "重试";
            Stretch(label.rectTransform);
            var button = retry.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => RetryClicked?.Invoke());
            _retry = retry.gameObject;

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            _panel.SetActive(false);
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
