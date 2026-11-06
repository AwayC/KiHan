using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KiHan.UI
{
    /// <summary>
    /// 页签 (通用部件)：选中时显示底图 (@Bg)、文字 (@Text) 变成选中色；未选中隐藏底图、文字恢复预制体里的颜色
    /// 底图隐藏只把透明度置 0，仍接收点击，整个页签区域都能点；点击只发 Clicked，切页由使用方处理
    /// </summary>
    public partial class PageTabView : UIView, IPointerClickHandler
    {
        [Tooltip("选中时的文字颜色")]
        [SerializeField] private Color _selectedTextColor = Color.white;

        /// <summary>
        /// 页签被点击 (已选中时也会触发)
        /// </summary>
        public event Action Clicked;

        private Color _normalTextColor;
        private bool _inited;

        private void Awake()
        {
            EnsureInit();
        }

        public void SetSelected(bool selected)
        {
            EnsureInit();
            _bg.canvasRenderer.SetAlpha(selected ? 1f : 0f);
            _text.color = selected ? _selectedTextColor : _normalTextColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }

        private void EnsureInit()
        {
            if (_inited) return;
            _inited = true;
            _normalTextColor = _text.color;
        }
    }
}
