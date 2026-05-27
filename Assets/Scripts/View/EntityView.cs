using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;

namespace View
{
    /// <summary>
    /// 通用表现层实体基类
    /// 处理插值、动画更新与渲染排序，不包含任何业务(如阴影)特有逻辑
    /// </summary>
    public class EntityView : MonoBehaviour
    {
        public LogicEntity BindEntity;
        
        [Header("平滑插值相关")]
        public float SmoothTime = 0.05f; 
        
        protected SpriteRenderer _mainSr;
        protected Transform _displayRoot;
        protected List<SpriteRenderer> _extraSrs = new List<SpriteRenderer>();

        protected AnimationFrameData _lastAnim;
        protected int _lastAnimVersion = -1;
        protected float _visualTimer = 0f;
        protected int _visualFrameIndex = 0;
        protected float _visualHeight = 0f; 

        protected Vector3 _posVelocity = Vector3.zero;
        protected float _heightVelocity = 0f;

        /// <summary>
        /// 瞬间同步位置并重置表现层动画状态，通常在对象池复用时调用
        /// </summary>
        public void SnapToEntityAndReset()
        {
            if (BindEntity != null)
            {
                transform.position = new Vector3(BindEntity.pos.x, BindEntity.pos.y, 0);
                _visualHeight = BindEntity.height * 0.01f;
                _posVelocity = Vector3.zero;
                _heightVelocity = 0f;

                // 强制重置动画相关参数
                _lastAnim = null;
                _lastAnimVersion = -1;
                _visualTimer = 0f;
                _visualFrameIndex = 0;
            }
        }

        protected virtual void Awake()
        {
            GameObject displayGo = new GameObject("Display");
            _displayRoot = displayGo.transform;
            _displayRoot.SetParent(this.transform);
            _displayRoot.localPosition = Vector3.zero;

            _mainSr = displayGo.AddComponent<SpriteRenderer>();
        }

        protected virtual void Start()
        {
            if (BindEntity != null && Managers.ViewManager.Instance != null)
            {
                Managers.ViewManager.Instance.RegisterView(BindEntity, _displayRoot, this);
            }
        }

        protected virtual void OnDestroy()
        {
            if (BindEntity != null && Managers.ViewManager.Instance != null)
            {
                Managers.ViewManager.Instance.UnregisterView(BindEntity);
            }
        }

        protected virtual void LateUpdate()
        {
            if (BindEntity == null) return;
            
            // 1. 位置平滑
            Vector3 targetPos = new Vector3(BindEntity.pos.x, BindEntity.pos.y, 0);
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVelocity, SmoothTime);

            // 1.1 高度平滑
            float targetHeight = BindEntity.height * 0.01f;
            if (targetHeight <= 0)
            {
                // 逻辑落地时，取消平滑阻尼，改为极高速度的线性坠落
                _visualHeight = Mathf.MoveTowards(_visualHeight, 0f, Time.deltaTime * 25f);
                _heightVelocity = 0f;
            }
            else
            {
                _visualHeight = Mathf.SmoothDamp(_visualHeight, targetHeight, ref _heightVelocity, SmoothTime);
            }

            // 2. 动画索引推进
            UpdateVisualIndex();

            // 3. 渲染
            RenderCurrent();
        }

        protected virtual void UpdateVisualIndex()
        {
            var currentAnim = BindEntity.CurrAnim;
            if (currentAnim == null || currentAnim.Steps.Count == 0) return;

            if (currentAnim != _lastAnim || BindEntity.AnimVersion != _lastAnimVersion)
            {
                _lastAnim = currentAnim;
                _lastAnimVersion = BindEntity.AnimVersion;
                _visualTimer = 0f;
                _visualFrameIndex = BindEntity.CurrentFrameIndex;
                return;
            }

            _visualTimer += Time.deltaTime;
            float renderTickTime = GameConfig.RENDER_TICK_TIME;

            bool isLooping = (currentAnim.IsLoop && !BindEntity.ForceNotLoop) || BindEntity.ForceLoop;
            
            if (_visualFrameIndex >= currentAnim.Steps.Count) _visualFrameIndex = 0;
            
            var step = currentAnim.Steps[_visualFrameIndex];
            if (_visualTimer >= step.Duration * renderTickTime)
            {
                if (_visualFrameIndex >= BindEntity.CurrentFrameIndex && !isLooping)
                {
                    // 等待逻辑层先行
                    _visualTimer = step.Duration * renderTickTime; 
                    return;
                }

                _visualTimer = 0;
                int nextIndex = _visualFrameIndex + 1;
                if (nextIndex < currentAnim.Steps.Count)
                {
                    _visualFrameIndex = nextIndex;
                }
                else if (isLooping)
                {
                    _visualFrameIndex = 0;
                }
                else
                {
                    _visualFrameIndex = currentAnim.Steps.Count - 1;
                }
            }
        }

        protected virtual void RenderCurrent()
        {
            var currentAnim = BindEntity.CurrAnim;
            if (currentAnim == null) return;

            var frameData = currentAnim.GetCurrentFrameData(_visualFrameIndex);
            if (frameData == null) return;

            float p2u = 0.01f;

            // 动态计算渲染层级
            int baseOrder = (2000 - Mathf.RoundToInt(BindEntity.pos.y * 100f)) * 2 + BindEntity.owner;
            _mainSr.sortingOrder = baseOrder;

            _mainSr.sprite = frameData.Sprite;
            _mainSr.flipX = BindEntity.IsFacingLeft;
            
            float offX = (_mainSr.flipX ? -frameData.Offset.x : frameData.Offset.x) * p2u;
            float offY = frameData.Offset.y * p2u;
            
            _displayRoot.localPosition = new Vector3(offX, offY + _visualHeight, 0);

            RenderExtraLayers(frameData, p2u);
            
            OnRenderComplete(baseOrder);
        }

        protected virtual void RenderExtraLayers(SpriteFrameData frameData, float p2u)
        {
            int layerCount = frameData.ExtraLayers.Count;
            while (_extraSrs.Count < layerCount)
            {
                GameObject go = new GameObject($"Layer_{_extraSrs.Count}");
                go.transform.SetParent(_displayRoot);
                _extraSrs.Add(go.AddComponent<SpriteRenderer>());
            }

            for (int i = 0; i < _extraSrs.Count; i++)
            {
                var sr = _extraSrs[i];
                if (i < layerCount)
                {
                    var layer = frameData.ExtraLayers[i];
                    sr.gameObject.SetActive(true);
                    sr.sprite = layer.Sprite;
                    sr.color = layer.TintColor;
                    sr.flipX = _mainSr.flipX;
                    sr.sortingOrder = _mainSr.sortingOrder + layer.OrderOffset;
                    float lx = (_mainSr.flipX ? -layer.Offset.x : layer.Offset.x) * p2u;
                    float ly = layer.Offset.y * p2u;
                    sr.transform.localPosition = new Vector3(lx, ly, 0);
                }
                else sr.gameObject.SetActive(false);
            }
        }

        // 提供给子类扩展的接口，例如渲染阴影
        protected virtual void OnRenderComplete(int baseOrder) { }

        public SpriteFrameData GetCurrentVisualFrame()
        {
            if (BindEntity == null || BindEntity.CurrAnim == null) return null;
            return BindEntity.CurrAnim.GetCurrentFrameData(_visualFrameIndex);
        }
    }
}
