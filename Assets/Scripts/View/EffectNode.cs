using UnityEngine;
using KiHan.Logic;
using System.Collections;

namespace View
{
    /// <summary>
    /// 特效表现层节点
    /// 挂载在特效预制体上，负责动画播放完后自动回收
    /// </summary>
    public class EffectNode : MonoBehaviour
    {
        public int EffectId { get; private set; }
        public string PoolKey; // 记录是从哪个池子生成的，用于回收
        
        private Animator _animator;
        private bool _isRecycled = false;

        private LogicEntity _bindEntity;
        private KiHan.Logic.EffectData _data;

        private SpriteRenderer[] _srs;
        private int[] _originalSortingOrders;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
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

            // 恢复所有渲染器可见性（防止上次回收时被隐藏）
            if (_srs != null)
            {
                foreach (var sr in _srs)
                {
                    if (sr != null) sr.enabled = true;
                }
            }

            // 1. 处理镜像翻转
            // 原生动画如果需要左右翻转，最简单的方法是反转 Scale X
            Vector3 scale = transform.localScale;
            scale.x = data.IsFacingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;

            // 2. 检查绑定逻辑
            if (_bindEntity != null)
            {
                // 向 ViewManager 索取该实体对应的表现层根节点
                Transform targetViewRoot = Managers.ViewManager.Instance.GetViewRoot(_bindEntity);
                if (targetViewRoot != null)
                {
                    // 设置为子物体，完美继承主体的位移平滑
                    transform.SetParent(targetViewRoot);
                    UpdateAnchoredPosition();
                }
                else
                {
                    // 容错：如果找不到 ViewRoot，则退化为基于绝对坐标的世界特效
                    transform.SetParent(null);
                    float p2u = 0.01f;
                    transform.position = new Vector3(_bindEntity.pos.x + data.Offset.x * p2u, _bindEntity.pos.y + data.Offset.y * p2u, 0);
                    UpdateSortingOrder(_bindEntity.pos.y);
                }
            }
            else
            {
                // 非绑定特效，直接放在世界坐标
                transform.SetParent(null);
                transform.position = new Vector3(data.WorldPos.x, data.WorldPos.y, 0);
                UpdateSortingOrder(data.WorldPos.y * 100f); // 假设传入的是真实的Unity坐标，转回逻辑y。如果传入的是逻辑y则不需要*100。不过统一处理也行
            }

            // 3. 监听动画结束自动销毁
            if (_animator != null)
            {
                // 强制重置动画并立即更新一帧，防止对象池复用时闪烁老残影
                _animator.Play(0, -1, 0f);
                _animator.Update(0f);
                
                StartCoroutine(WaitAndRecycle());
            }
            else
            {
                // 如果没有 Animator，设置一个默认存活时间防泄漏
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
            // 给个偏移，比如 10，让它盖在角色(及额外层)上面
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

                    // 查找指定名称的锚点
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

                    // 实时同步主体的朝向，因为特效在播放过程中主体可能转身
                    Vector3 scale = transform.localScale;
                    scale.x = _bindEntity.IsFacingLeft ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                    transform.localScale = scale;

                    // 实时更新渲染图层，跟随角色层级
                    UpdateSortingOrder(_bindEntity.pos.y);
                }
            }
        }

        private IEnumerator WaitAndRecycle()
        {
            yield return null; 
            
            // 逐帧检测动画播放进度，接近 1.0 时立刻回收，防止 Animator 循环导致闪烁第一帧
            while (true)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 0.95f)
                {
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
