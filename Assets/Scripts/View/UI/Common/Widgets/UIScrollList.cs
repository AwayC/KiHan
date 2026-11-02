using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KiHan.UI
{
    /// <summary>
    /// 通用滑动列表 (横向 / 纵向)：拖动跟手，松手按惯性减速，越界时橡皮筋阻尼、松手弹性回弹；可选停靠 (松手停在最近一项并居中，轻甩翻一项)
    /// - 只创建可见范围内的项并循环复用，项数量不限；项由模板 (_template：_content 下隐藏的子节点，大小即项大小) 复制
    /// - 用法：SetCount(数量, (项部件, 下标) =&gt; 绑定)；某项进入可见范围或调用 RefreshItems 时回调绑定；ScrollTo 定位
    /// - 节点约定：本节点即视口 (挂 RectMask2D 裁剪、透明 Image 接收空白处的拖动)，_content 铺满视口，项锚定在视口中心
    /// - 按 unscaledDeltaTime 推进 (暂停时也能滑动)，静止时不刷新布局；拖动超过阈值后不触发项内按钮的点击
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIScrollList : UIView, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public enum Direction
        {
            Horizontal,
            Vertical,
        }

        /// <summary>
        /// 轻甩翻页的最低松手速度 (像素 / 秒)
        /// </summary>
        private const float FlickSpeed = 400f;

        /// <summary>
        /// 视为停止的速度 (像素 / 秒) 与距离 (像素)
        /// </summary>
        private const float RestSpeed = 1f;
        private const float RestDistance = 0.5f;

        [SerializeField] private Direction _direction = Direction.Horizontal;

        [Tooltip("项的父节点 (铺满视口)")]
        [SerializeField] private RectTransform _content;

        [Tooltip("项模板：_content 下隐藏的子节点，需挂项部件 (SetCount 的 TView)，大小即项大小")]
        [SerializeField] private RectTransform _template;

        [Tooltip("项间距 (参考分辨率像素)")]
        [SerializeField] private float _spacing = 40f;

        [Tooltip("首尾留白 (参考分辨率像素)")]
        [SerializeField] private float _padding = 40f;

        [Header("手感")]
        [Tooltip("停靠：松手后停在最近一项并居中，轻甩翻一项")]
        [SerializeField] private bool _snap;

        [Tooltip("惯性：每秒保留的速度比例，越小停得越快")]
        [SerializeField, Range(0.01f, 0.9f)] private float _decelerationRate = 0.135f;

        [Tooltip("越界回弹 / 停靠 / ScrollTo 动画的平滑时间 (秒)")]
        [SerializeField] private float _springTime = 0.15f;

        [Tooltip("离开中心一项距离时的缩放 (1 = 不缩放；停靠模式常用 0.85)")]
        [SerializeField, Range(0.5f, 1f)] private float _sideScale = 1f;

        [Tooltip("鼠标滚轮每格滚动距离 (停靠模式下每格翻一项)")]
        [SerializeField] private float _wheelStep = 120f;

        private sealed class Item
        {
            public RectTransform Rect;
            public Component View;
            public int Index;
        }

        /// <summary>
        /// 最靠近视口中心的项变化时触发 (参数为下标)
        /// </summary>
        public event Action<int> CenterChanged;

        public int Count => _count;

        /// <summary>
        /// 最靠近视口中心的项 (列表为空时为 -1)
        /// </summary>
        public int CenterIndex => _centerIndex;

        private readonly List<Item> _active = new List<Item>();
        private readonly Stack<Item> _pool = new Stack<Item>();
        private Action<Component, int> _bind;
        private Type _viewType;
        private RectTransform _viewport;
        private Vector3 _itemScale = Vector3.one;
        private int _count;
        private int _centerIndex = -1;

        // 滚动位置：视口起点 (横向为左边、纵向为上边) 在列表坐标中的位置，列表坐标从第一项前的留白起算
        private float _pos;
        private float _prevPos;
        private float _velocity;
        private bool _dragging;
        private bool _hasTarget;
        private float _target;
        private Vector2 _dragStartPointer;
        private float _dragStartPos;
        private bool _dirty;

        private void Awake()
        {
            EnsureInit();
        }

        /// <summary>
        /// 缓存视口并隐藏模板 (SetCount 可能早于 Awake 调用，如面板在未激活时绑定数据)
        /// </summary>
        private void EnsureInit()
        {
            if (_viewport != null) return;
            _viewport = (RectTransform)transform;
            if (_template != null)
            {
                _itemScale = _template.localScale;
                _template.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            _dragging = false;
        }

        private void OnRectTransformDimensionsChange()
        {
            _dirty = true;
        }

        #region 对外接口

        /// <summary>
        /// 设置项数量与绑定回调：回收全部项后按当前位置重新布局并绑定可见项 (位置收回到有效范围内)
        /// </summary>
        public void SetCount<TView>(int count, Action<TView, int> bind) where TView : Component
        {
            if (_template == null || _content == null)
            {
                Debug.LogError($"[UIScrollList] {name} 未配置 _template 或 _content");
                return;
            }
            EnsureInit();

            if (_viewType != typeof(TView))
            {
                // 项部件类型变化：旧实例上的部件不可复用
                ClearInstances();
                _viewType = typeof(TView);
            }
            _bind = (view, index) => bind((TView)view, index);

            RecycleAll();
            _count = Mathf.Max(0, count);
            _velocity = 0f;
            _hasTarget = false;
            _dragging = false;
            Bounds(out float min, out float max);
            _pos = Mathf.Clamp(_pos, min, max);
            _prevPos = _pos;
            _centerIndex = -1;
            Layout();
        }

        /// <summary>
        /// 重新绑定全部可见项 (数据变化、数量不变时使用)
        /// </summary>
        public void RefreshItems()
        {
            if (_bind == null) return;
            foreach (Item item in _active) _bind(item.View, item.Index);
        }

        /// <summary>
        /// 滚动到第 index 项 (尽量居中，不越界)；animated 为 false 时立即定位
        /// </summary>
        public void ScrollTo(int index, bool animated)
        {
            if (_count == 0) return;
            index = Mathf.Clamp(index, 0, _count - 1);
            Bounds(out float min, out float max);
            float target = Mathf.Clamp(CenteredPos(index), min, max);
            _velocity = 0f;
            if (animated)
            {
                _target = target;
                _hasTarget = true;
                return;
            }

            _hasTarget = false;
            _pos = target;
            _prevPos = target;
            Layout();
        }

        #endregion

        #region 拖动

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 按下即停住惯性与动画 (与原生 ScrollRect 一致)
            _velocity = 0f;
            _hasTarget = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _count == 0) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera, out _dragStartPointer)) return;
            _dragging = true;
            _hasTarget = false;
            _velocity = 0f;
            _dragStartPos = _pos;
            _prevPos = _pos;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;

            Vector2 delta = local - _dragStartPointer;
            // 横向：手指右移内容右移 (位置减小)；纵向：手指上移内容上移 (位置增大)
            float raw = _dragStartPos + (_direction == Direction.Horizontal ? -delta.x : delta.y);
            Bounds(out float min, out float max);
            float view = ViewLength;
            if (raw < min) raw = min + RubberBand(raw - min, view);
            else if (raw > max) raw = max + RubberBand(raw - max, view);
            SetPos(raw);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = false;
            if (_snap) BeginSnap();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_count == 0) return;
            Vector2 wheel = eventData.scrollDelta;
            // 滚轮上滚回到开头；触控板横向滚动按横向位移
            float step = Mathf.Abs(wheel.x) > Mathf.Abs(wheel.y) ? wheel.x : -wheel.y;
            if (step == 0f) return;

            float from = _hasTarget ? _target : _pos;
            if (_snap)
            {
                int index = Mathf.Clamp(NearestIndex(from) + (step > 0f ? 1 : -1), 0, _count - 1);
                _target = CenteredPos(index);
            }
            else
            {
                Bounds(out float min, out float max);
                _target = Mathf.Clamp(from + step * _wheelStep, min, max);
            }
            _velocity = 0f;
            _hasTarget = true;
        }

        /// <summary>
        /// 停靠：按惯性滑行的终点选最近一项；速度足够但终点仍是当前项时翻一项 (轻甩)
        /// </summary>
        private void BeginSnap()
        {
            // 速度按 r^t 指数衰减，滑行总距离 = v / -ln(r)
            float travel = _velocity / -Mathf.Log(_decelerationRate);
            int index = NearestIndex(_pos + travel);
            if (index == NearestIndex(_pos) && Mathf.Abs(_velocity) > FlickSpeed)
            {
                index = Mathf.Clamp(index + (_velocity > 0f ? 1 : -1), 0, _count - 1);
            }
            _target = CenteredPos(index);
            _hasTarget = true;
        }

        /// <summary>
        /// 越界拉伸的阻尼：拉得越远越难拉，最多接近一个视口长度 (与原生 ScrollRect 相同的曲线)
        /// </summary>
        private static float RubberBand(float overStretch, float viewSize)
        {
            if (viewSize <= 0f) return 0f;
            return (1f - 1f / (Mathf.Abs(overStretch) * 0.55f / viewSize + 1f)) * viewSize * Mathf.Sign(overStretch);
        }

        #endregion

        #region 运动

        private void LateUpdate()
        {
            if (_count == 0 || _bind == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            if (_dragging)
            {
                // 跟手期间平滑估计速度，松手后作为惯性初速度
                float instant = (_pos - _prevPos) / dt;
                _velocity = Mathf.Lerp(_velocity, instant, dt * 10f);
            }
            else if (_hasTarget)
            {
                float pos = Mathf.SmoothDamp(_pos, _target, ref _velocity, _springTime, Mathf.Infinity, dt);
                if (Mathf.Abs(_target - pos) < RestDistance && Mathf.Abs(_velocity) < RestSpeed * 10f)
                {
                    pos = _target;
                    _velocity = 0f;
                    _hasTarget = false;
                }
                SetPos(pos);
            }
            else
            {
                Bounds(out float min, out float max);
                if (_pos < min || _pos > max)
                {
                    // 越界：带着当前速度弹回边界 (先冲出一段再回来，弹性的来源)
                    float bound = _pos < min ? min : max;
                    float pos = Mathf.SmoothDamp(_pos, bound, ref _velocity, _springTime, Mathf.Infinity, dt);
                    if (Mathf.Abs(bound - pos) < RestDistance && Mathf.Abs(_velocity) < RestSpeed * 10f)
                    {
                        pos = bound;
                        _velocity = 0f;
                    }
                    SetPos(pos);
                }
                else if (_velocity != 0f)
                {
                    _velocity *= Mathf.Pow(_decelerationRate, dt);
                    if (Mathf.Abs(_velocity) < RestSpeed) _velocity = 0f;
                    SetPos(_pos + _velocity * dt);
                }
            }

            _prevPos = _pos;
            if (_dirty) Layout();
        }

        private void SetPos(float pos)
        {
            if (pos == _pos) return;
            _pos = pos;
            _dirty = true;
        }

        #endregion

        #region 布局

        private bool Horizontal => _direction == Direction.Horizontal;

        private float ViewLength => Horizontal ? _viewport.rect.width : _viewport.rect.height;

        private float ItemLength => Horizontal ? _template.rect.width : _template.rect.height;

        private float Stride => ItemLength + _spacing;

        /// <summary>
        /// 第 i 项中心在列表坐标中的位置
        /// </summary>
        private float ItemCenter(int i) => _padding + i * Stride + ItemLength * 0.5f;

        /// <summary>
        /// 第 i 项居中时的滚动位置
        /// </summary>
        private float CenteredPos(int i) => ItemCenter(i) - ViewLength * 0.5f;

        /// <summary>
        /// 滚动范围：停靠模式为首项居中到末项居中；否则为内容首尾贴边，内容不足一屏时整体居中且不可滚动
        /// </summary>
        private void Bounds(out float min, out float max)
        {
            if (_count == 0 || _template == null)
            {
                min = max = 0f;
                return;
            }

            if (_snap)
            {
                min = CenteredPos(0);
                max = CenteredPos(_count - 1);
                return;
            }

            float view = ViewLength;
            float content = 2f * _padding + _count * ItemLength + (_count - 1) * _spacing;
            if (content <= view)
            {
                min = max = (content - view) * 0.5f;
                return;
            }
            min = 0f;
            max = content - view;
        }

        private int NearestIndex(float pos)
        {
            if (_count == 0) return -1;
            int index = Mathf.RoundToInt((pos + ViewLength * 0.5f - _padding - ItemLength * 0.5f) / Stride);
            return Mathf.Clamp(index, 0, _count - 1);
        }

        private void Layout()
        {
            _dirty = false;
            if (_bind == null) return;

            float view = ViewLength;
            float size = ItemLength;
            float stride = Stride;
            int first = 0;
            int last = -1;
            if (_count > 0 && stride > 0f)
            {
                first = Mathf.Max(0, Mathf.CeilToInt((_pos - _padding - size) / stride));
                last = Mathf.Min(_count - 1, Mathf.FloorToInt((_pos + view - _padding) / stride));
            }

            // 回收离开可见范围的项
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Item item = _active[i];
                if (item.Index >= first && item.Index <= last) continue;
                Recycle(item);
                _active.RemoveAt(i);
            }

            for (int index = first; index <= last; index++)
            {
                Item item = FindActive(index);
                if (item == null)
                {
                    item = Spawn();
                    item.Index = index;
                    _active.Add(item);
                    _bind(item.View, index);
                }

                float offset = ItemCenter(index) - _pos - view * 0.5f;
                item.Rect.anchoredPosition = Horizontal ? new Vector2(offset, 0f) : new Vector2(0f, -offset);
                if (_sideScale < 1f)
                {
                    float t = Mathf.Clamp01(Mathf.Abs(offset) / stride);
                    item.Rect.localScale = _itemScale * Mathf.Lerp(1f, _sideScale, t);
                }
            }

            int center = NearestIndex(_pos);
            if (center == _centerIndex) return;
            _centerIndex = center;
            CenterChanged?.Invoke(center);
        }

        private Item FindActive(int index)
        {
            foreach (Item item in _active)
            {
                if (item.Index == index) return item;
            }
            return null;
        }

        private Item Spawn()
        {
            Item item;
            if (_pool.Count > 0)
            {
                item = _pool.Pop();
            }
            else
            {
                RectTransform rect = Instantiate(_template, _content);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                Component view = rect.GetComponent(_viewType);
                if (view == null) Debug.LogError($"[UIScrollList] {name} 的模板上没有 {_viewType.Name}");
                item = new Item { Rect = rect, View = view };
            }
            item.Rect.localScale = _itemScale;
            item.Rect.gameObject.SetActive(true);
            return item;
        }

        private void Recycle(Item item)
        {
            item.Index = -1;
            item.Rect.gameObject.SetActive(false);
            _pool.Push(item);
        }

        private void RecycleAll()
        {
            foreach (Item item in _active) Recycle(item);
            _active.Clear();
        }

        private void ClearInstances()
        {
            RecycleAll();
            while (_pool.Count > 0) Destroy(_pool.Pop().Rect.gameObject);
        }

        #endregion
    }
}
