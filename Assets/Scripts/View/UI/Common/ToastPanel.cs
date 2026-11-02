using System.Collections.Generic;
using UnityEngine;

namespace KiHan.UI
{
    /// <summary>
    /// 飘字提示 (UIManager.Toast)：消息排队依次显示，每条淡入 0.2 秒、停留 1.5 秒、淡出 0.2 秒，全部显示完后关闭自己；不挡点击
    /// </summary>
    [UIPanel("UI/Panels/Common/ToastPanel", UILayer.Toast)]
    public partial class ToastPanel : UIPanel
    {
        private const float FadeSec = 0.2f;
        private const float HoldSec = 1.5f;

        private readonly Queue<string> _queue = new Queue<string>();
        private CanvasGroup _group;
        private float _elapsed = -1f;

        protected override void OnCreate()
        {
            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        protected override void OnOpen()
        {
            _group.alpha = 0f;
        }

        protected override void OnClose()
        {
            _queue.Clear();
            _elapsed = -1f;
        }

        /// <summary>
        /// 加入一条消息 (当前没有在显示时立即显示)
        /// </summary>
        public void Enqueue(string message)
        {
            _queue.Enqueue(message ?? string.Empty);
            if (_elapsed < 0f) ShowNext();
        }

        private void ShowNext()
        {
            if (_queue.Count == 0)
            {
                _elapsed = -1f;
                Close();
                return;
            }
            _text.text = _queue.Dequeue();
            _elapsed = 0f;
            _group.alpha = 0f;
        }

        private void Update()
        {
            if (_elapsed < 0f) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < FadeSec) _group.alpha = _elapsed / FadeSec;
            else if (_elapsed < FadeSec + HoldSec) _group.alpha = 1f;
            else if (_elapsed < FadeSec * 2f + HoldSec) _group.alpha = 1f - (_elapsed - FadeSec - HoldSec) / FadeSec;
            else ShowNext();
        }
    }
}
