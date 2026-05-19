using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

namespace Managers
{
    // 按钮点击缩放效果
    public class UIButtonScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 _originalScale = Vector3.one;
        private bool _isInit = false;
        
        private void Init()
        {
            if (!_isInit)
            {
                _originalScale = transform.localScale;
                _isInit = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Init();
            transform.localScale = _originalScale * 0.9f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }
    }

    // 面板打开/关闭缩放效果
    public static class UIPanelAnim
    {
        public static void Show(MonoBehaviour runner, GameObject panel, float duration = 0.2f)
        {
            if (panel == null) return;
            panel.SetActive(true); 
            // 停止目标身上可能存在的隐藏协程（如果有）
            runner.StartCoroutine(DoScale(panel.transform, Vector3.one * 0.8f, Vector3.one, duration));
        }

        public static void Hide(MonoBehaviour runner, GameObject panel, float duration = 0.15f)
        {
            if (panel == null || !panel.activeInHierarchy) return;
            runner.StartCoroutine(DoHide(panel, duration));
        }

        private static IEnumerator DoScale(Transform target, Vector3 startScale, Vector3 endScale, float duration)
        {
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

            float time = 0;
            target.localScale = startScale;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                float easeT = 1 - Mathf.Pow(1 - t, 3); // easeOutCubic
                target.localScale = Vector3.Lerp(startScale, endScale, easeT);
                cg.alpha = Mathf.Lerp(0f, 1f, easeT);
                yield return null;
            }
            target.localScale = endScale;
            cg.alpha = 1f;
        }

        private static IEnumerator DoHide(GameObject panel, float duration)
        {
            Transform target = panel.transform;
            Vector3 startScale = target.localScale;
            Vector3 endScale = Vector3.one * 0.8f;
            
            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

            float time = 0;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                // 使用 easeInCubic 曲线：先慢后快，关闭时更干脆自然
                float easeT = t * t * t; 
                target.localScale = Vector3.Lerp(startScale, endScale, easeT);
                cg.alpha = Mathf.Lerp(1f, 0f, easeT);
                yield return null;
            }
            panel.SetActive(false);
            target.localScale = Vector3.one; // 为下次打开重置比例
            cg.alpha = 1f; // 为下次重置透明度
        }
    }

    public enum SlideDir { Top, Bottom, Left, Right }

    public static class UISlideAnim
    {
        public static IEnumerator DoSlide(RectTransform target, Vector2 originalPos, SlideDir dir, bool isShow, float duration = 0.3f, float offsetDist = 1000f)
        {
            if (target == null) yield break;

            Vector2 offset = Vector2.zero;
            switch (dir)
            {
                case SlideDir.Top: offset = new Vector2(0, offsetDist); break;
                case SlideDir.Bottom: offset = new Vector2(0, -offsetDist); break;
                case SlideDir.Left: offset = new Vector2(-offsetDist, 0); break;
                case SlideDir.Right: offset = new Vector2(offsetDist, 0); break;
            }

            Vector2 startPos = isShow ? (originalPos + offset) : originalPos;
            Vector2 endPos = isShow ? originalPos : (originalPos + offset);

            CanvasGroup cg = target.GetComponent<CanvasGroup>();
            if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

            float time = 0;
            target.anchoredPosition = startPos;
            
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                
                // 打开时 easeOut (越到目标越慢), 关闭时 easeIn (越到目标越快)
                float easeT = isShow ? (1 - Mathf.Pow(1 - t, 3)) : (t * t * t);
                
                target.anchoredPosition = Vector2.Lerp(startPos, endPos, easeT);
                cg.alpha = isShow ? Mathf.Lerp(0f, 1f, easeT) : Mathf.Lerp(1f, 0f, easeT);
                yield return null;
            }
            
            target.anchoredPosition = endPos;
            cg.alpha = isShow ? 1f : 0f;
        }
    }
}