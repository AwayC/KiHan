using UnityEngine;
using KiHan.Logic;
using System.Collections;
using UnityEngine.Playables;

namespace View
{
    /// <summary>
    /// 特效表现层 node
    /// 挂载在特效预制体上，负责动画播放完后自动回收
    /// </summary>
    public class EffectNode : MonoBehaviour
    {
        public int EffectId { get; private set; }
        public string PoolKey; // 记录是从哪个池子生成的，用于回收
        
        private Animator _animator;
        private PlayableDirector _director;
        private bool _isRecycled = false;

        private LogicEntity _bindEntity;
        private KiHan.Logic.EffectData _data;

        private SpriteRenderer[] _srs;
        private int[] _originalSortingOrders;

        private void Awake()
        {
            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            _director = GetComponent<PlayableDirector>() ?? GetComponentInChildren<PlayableDirector>();
            _srs = GetComponentsInChildren<SpriteRenderer>(true);
            if (_srs != null)
            {
                _originalSortingOrders = new int[_srs.Length];
                for (int i = 0; i < _srs.Length; i++)
                {
                    _originalSortingOrders[i] = _srs[i].sortingOrder;
                }
            }
        }

        public void Init(int id, KiHan.Logic.EffectData data)
        {
            EffectId = id;
            _isRecycled = false;
            _bindEntity = data.BindEntity;
            _data = data;

            // 调试：改个名字方便您在 Hierarchy 视图中找到它
            gameObject.name = $"[Effect]_{EffectId}_{PoolKey}";

            // 重新查找组件（防止某些特效是动态挂载的）
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_director == null) _director = GetComponentInChildren<PlayableDirector>();

            // 恢复所有渲染器可见性（防止上次回收时被隐藏）
            if (_srs != null)
            {
                foreach (var sr in _srs)
                {
                    if (sr != null) sr.enabled = true;
                }
            }

            // 1. 处理镜像翻转
            Vector3 scale = transform.localScale;
            scale.x = data.IsFacingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
            data.Offset.x *= data.IsFacingLeft ? 1 : -1;

            // 2. 检查绑定逻辑
            if (_bindEntity != null)
            {
                Transform targetViewRoot = Managers.ViewManager.Instance.GetViewRoot(_bindEntity);
                if (targetViewRoot != null)
                {
                    transform.SetParent(targetViewRoot);
                    UpdateAnchoredPosition();
                    Debug.Log($"[Effect] {gameObject.name} 已挂载到实体视图: {targetViewRoot.parent.name}");
                }
                else
                {
                    // 容错：如果找不到 ViewRoot，则退化为基于绝对坐标的世界特效
                    transform.SetParent(null);
                    float p2u = 0.01f;
                    transform.position = new Vector3(_bindEntity.pos.x + data.Offset.x * p2u, _bindEntity.pos.y + (_bindEntity.height + data.Offset.y) * p2u, 0);
                    UpdateSortingOrder(_bindEntity.pos.y);
                }
            }
            else
            {
                // 非绑定特效，放在世界坐标，同时应用 Offset 和 Height
                transform.SetParent(null);
                float p2u = 0.01f;
                transform.position = new Vector3(data.WorldPos.x + data.Offset.x * p2u, data.WorldPos.y + (data.Height + data.Offset.y) * p2u, 0);
                UpdateSortingOrder(data.WorldPos.y * 100f);
            }

            // 3. 监听动画结束自动销毁
            if (_animator != null || _director != null)
            {
                if (_animator != null)
                {
                    _animator.Play(0, -1, 0f);
                    _animator.Update(0f);
                }
                
                if (_director != null)
                {
                    _director.time = 0;
                    _director.Evaluate(); 
                    _director.Play();
                }

                StartCoroutine(WaitAndRecycle());
            }
            else
            {
                Debug.LogWarning($"[Effect] {gameObject.name} 未找到控制器，将在 2 秒后自动回收。");
                Invoke(nameof(Recycle), 2.0f);
            }
        }

        private void LateUpdate()
        {
            if (_isRecycled || _bindEntity == null) return;
            UpdateAnchoredPosition();
        }

        private void UpdateSortingOrder(float logicY)
        {
            int baseOrder = (2000 - Mathf.RoundToInt(logicY)) * 2;
            if (_bindEntity != null)
            {
                baseOrder += _bindEntity.owner;
            }
            int effectBaseOrder = baseOrder + 10;
            
            if (_srs != null)
            {
                for (int i = 0; i < _srs.Length; i++)
                {
                    if (_srs[i] != null)
                    {
                        _srs[i].sortingOrder = effectBaseOrder + _originalSortingOrders[i];
                    }
                }
            }
        }

        private void UpdateAnchoredPosition()
        {
            var view = Managers.ViewManager.Instance.GetEntityView(_bindEntity);
            if (view != null)
            {
                var frame = view.GetCurrentVisualFrame();
                if (frame != null)
                {
                    float p2u = 0.01f;
                    Vector2 anchorPos = Vector2.zero;

                    if (!string.IsNullOrEmpty(_data.AnchorName) && frame.EffectAnchors != null)
                    {
                        var anchor = frame.EffectAnchors.Find(a => a.Name == _data.AnchorName);
                        if (anchor != null)
                        {
                            anchorPos = anchor.Position;
                        }
                    }

                    float baseAnchorX = anchorPos.x + _data.Offset.x - frame.Offset.x;
                    float baseAnchorY = anchorPos.y + _data.Offset.y - frame.Offset.y;

                    float offX = (_bindEntity.IsFacingLeft ? -baseAnchorX : baseAnchorX) * p2u;
                    float offY = baseAnchorY * p2u;

                    transform.localPosition = new Vector3(offX, offY, 0);

                    Vector3 scale = transform.localScale;
                    scale.x = _bindEntity.IsFacingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                    transform.localScale = scale;

                    UpdateSortingOrder(_bindEntity.pos.y);
                }
            }
        }

        private IEnumerator WaitAndRecycle()
        {
            float startTime = Time.time;
            
            // 延迟 2 帧，确保控制器状态同步
            yield return null; 
            yield return null;
            
            // 逐帧检测播放进度，完成时立刻回收
            while (true)
            {
                // 安全保底：如果 8 秒还没播完，强制回收，防止僵尸特效留在场上
                if (Time.time - startTime > 8.0f)
                {
                    Debug.LogWarning($"[Effect] {gameObject.name} 播放超时 (8秒)，执行保底回收。");
                    break;
                }

                bool isDone = false;

                // 核心修复：如果存在 Timeline，优先以 Timeline 的状态为准
                // 因为很多 Timeline 特效也会挂载 Animator 用于骨骼绑定，但 Animator 本身并不播放 Clip（时间永远是 0）
                if (_director != null)
                {
                    if (_director.state != PlayState.Playing)
                    {
                        isDone = true;
                    }
                }
                else if (_animator != null)
                {
                    AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    // 如果不是循环动画，normalizedTime >= 1.0 表示播放完毕
                    if (stateInfo.normalizedTime >= 0.98f) 
                    {
                        isDone = true;
                    }
                }
                else
                {
                    isDone = true; // 理论上走不到这里，Init 中有防御
                }

                if (isDone)
                {
                    Debug.Log($"[Effect] {gameObject.name} 检测到播放结束，准备回收。");
                    break;
                }
                
                yield return null;
            }

            // 回收前关闭所有 SpriteRenderer，彻底根除残影
            if (_srs != null)
            {
                foreach (var sr in _srs)
                {
                    if (sr != null) sr.enabled = false;
                }
            }

            Recycle();
        }

        public void Recycle()
        {
            if (_isRecycled) return;
            _isRecycled = true;
            
            // 清理对父节点的依赖
            transform.SetParent(null);
            
            // 通知管理器回收自己
            Managers.EffectManager.Instance.RecycleEffect(this);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            CancelInvoke();
        }
    }
}