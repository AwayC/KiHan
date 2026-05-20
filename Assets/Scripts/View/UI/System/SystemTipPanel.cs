using UnityEngine;
using TMPro;
using System.Collections;
using Managers;

namespace KiHan.View.UI.System
{
    public class SystemTipPanel : BasePanel
    {
        public override int SortingPriority => 9999;

        private TMP_Text _tipText;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            // 路径：Canvas/Background/Text
            _tipText = transform.Find("Canvas/Background/Text")?.GetComponent<TMP_Text>();
        }

        public override void OnOpen(object data = null)
        {
            // 不要调用 base.OnOpen，因为它会播放缩放动画，TipPanel 我们希望是淡入淡出
            gameObject.SetActive(true);
            
            if (data != null)
            {
                if (_tipText != null) _tipText.text = data.ToString();
            }

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(TipRoutine());
        }

        private IEnumerator TipRoutine()
        {
            if (_canvasGroup == null) yield break;

            // 1. 淡入
            float time = 0;
            float duration = 0.2f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, time / duration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            // 2. 停留
            yield return new WaitForSecondsRealtime(1.5f);

            // 3. 淡出
            time = 0;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, time / duration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;

            // 4. 关闭
            gameObject.SetActive(false);
        }
    }
}