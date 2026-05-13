using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;

public class PlayerView : MonoBehaviour
{
    public LogicEntity BindEntity;
    public float SmoothSpeed = 20f; // 回调到一个平衡点 (15~20 之间比较顺滑)

    
    private SpriteRenderer _mainSr;
    private Transform _displayRoot; // 专门负责美术偏移的节点
    private List<SpriteRenderer> _extraSrs = new List<SpriteRenderer>();

    private GameObject _shadowGo;
    private SpriteRenderer[] _shadowSrs;
    private int[] _shadowBaseOrders;

    private AnimationFrameData _lastAnim;
    private int _lastAnimVersion = -1; // 记录上一次同步的动画版本
    private float _visualTimer = 0f;
    private int _visualFrameIndex = 0;
    private float _visualHeight = 0f; 

    // --- 平滑插值相关 ---
    public float SmoothTime = 0.05f; 
    private Vector3 _posVelocity = Vector3.zero;
    private float _heightVelocity = 0f;

    private void Awake()
    {
        // 创建一个子节点专门处理美术偏移
        GameObject displayGo = new GameObject("Display");
        _displayRoot = displayGo.transform;
        _displayRoot.SetParent(this.transform);
        _displayRoot.localPosition = Vector3.zero;

        _mainSr = displayGo.AddComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (BindEntity == null) return;
        
        // 0. 初始化脚下圆盘阴影
        if (_shadowGo == null)
        {
            string shadowPath = $"UI/Shadow/shadow_{BindEntity.owner}";
            _shadowGo = Managers.ResManager.Instance.Spawn(shadowPath, Vector3.zero, Quaternion.identity, this.transform);
            
            if (_shadowGo != null)
            {
                _shadowGo.transform.localPosition = Vector3.zero;
                
                // 动态挂载动画脚本 (0.4倍压缩 & 转速差一倍)
                if (_shadowGo.GetComponent<ShadowEffect>() == null)
                {
                    var effect = _shadowGo.AddComponent<ShadowEffect>();
                    if(BindEntity.owner == 1)
                    {
                        effect.Speed1 = -180f;
                        effect.Speed2 = -225f; // 外圈和内圈速度相反且差一倍
                    } else
                    {
                        effect.Speed1 = effect.Speed2 = 180f;
                    }
                    
                }
                
                // 缓存渲染器和初始层级用于动态深度排序
                _shadowSrs = _shadowGo.GetComponentsInChildren<SpriteRenderer>();
                _shadowBaseOrders = new int[_shadowSrs.Length];
                for (int i = 0; i < _shadowSrs.Length; i++)
                {
                    _shadowBaseOrders[i] = _shadowSrs[i].sortingOrder;
                }
            }
        }

        // 1. 位置平滑
        Vector3 targetPos = new Vector3(BindEntity.pos.x, BindEntity.pos.y, 0);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _posVelocity, SmoothTime);

        // 1.1 高度平滑 (新增)
        float targetHeight = BindEntity.height * 0.01f;
        if (targetHeight <= 0)
        {
            // 逻辑落地时，取消平滑阻尼，改为极高速度的线性坠落（消除类似弹簧的悬浮感，增强砸地打击感）
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
    private void UpdateVisualIndex()
    {
        var currentAnim = BindEntity.CurrAnim;
        if (currentAnim == null || currentAnim.Steps.Count == 0) return;

        // 1. 动画资源改变 或 逻辑层版本号更新（显式重置）：重置视觉计时器
        if (currentAnim != _lastAnim || BindEntity.AnimVersion != _lastAnimVersion)
        {
            _lastAnim = currentAnim;
            _lastAnimVersion = BindEntity.AnimVersion;
            _visualTimer = 0f;
            _visualFrameIndex = BindEntity.CurrentFrameIndex;
            return;
        }

        // 3. 自驱动推进：表现层按 60fps 推进时间
        _visualTimer += Time.deltaTime;
        float renderTickTime = GameConfig.RENDER_TICK_TIME;

        bool isLooping = (currentAnim.IsLoop && !BindEntity.ForceNotLoop) || BindEntity.ForceLoop;        
        // 安全检查
        if (_visualFrameIndex >= currentAnim.Steps.Count) _visualFrameIndex = 0;
        
        var step = currentAnim.Steps[_visualFrameIndex];
        if (_visualTimer >= step.Duration * renderTickTime)
        {
            // 如果视觉层即将跳到下一帧，但逻辑层还被“钉”在当前帧（例如空中等待落地）
            // 我们就不允许视觉层继续往下播，强行锁在这帧
            if (_visualFrameIndex >= BindEntity.CurrentFrameIndex && !isLooping)
            {
                // 等待逻辑层先行
                _visualTimer = step.Duration * renderTickTime; // 卡在当前帧满状态
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

    private void RenderCurrent()
    {
        var currentAnim = BindEntity.CurrAnim;
        if (currentAnim == null) return;

        var frameData = currentAnim.GetCurrentFrameData(_visualFrameIndex);
        if (frameData == null) return;

        float p2u = 0.01f;

        // 动态计算渲染层级
        int baseOrder = (2000 - Mathf.RoundToInt(BindEntity.pos.y * 100f)) * 2 + BindEntity.owner;
        _mainSr.sortingOrder = baseOrder;

        // A. 渲染本体
        _mainSr.sprite = frameData.Sprite;
        _mainSr.flipX = BindEntity.IsFacingLeft;
        
        float offX = (_mainSr.flipX ? -frameData.Offset.x : frameData.Offset.x) * p2u;
        float offY = frameData.Offset.y * p2u;
        
        // 使用平滑后的 _visualHeight 替代原始的 logicHeight
        _displayRoot.localPosition = new Vector3(offX, offY + _visualHeight, 0);

        // B. 渲染特效图层
        RenderExtraLayers(frameData, p2u);

        // C. 更新脚下阴影图层
        UpdateShadows(baseOrder);
    }

    private void RenderExtraLayers(SpriteFrameData frameData, float p2u)
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

    private void UpdateShadows(int baseOrder)
    {
        if (_shadowSrs != null)
        {
            for (int i = 0; i < _shadowSrs.Length; i++)
            {
                _shadowSrs[i].sortingOrder = baseOrder - 20 + _shadowBaseOrders[i];
            }
        }
    }

    // --- 调试渲染判定盒 ---
    private void OnDrawGizmos()
    {
        if (BindEntity == null || BindEntity.CurrAnim == null) return;

        // 绘制受击盒 (蓝色)
        var hurtBoxes = BindEntity.CurrAnim.GetHurtBoxes(BindEntity.CurrentFrameIndex);
        if (hurtBoxes != null)
        {
            Gizmos.color = new Color(0, 0, 1, 0.4f);
            foreach (var box in hurtBoxes) DrawLogicBox(box);
        }

        // 绘制攻击盒 (红色)
        var hitBoxes = BindEntity.CurrAnim.GetHitBoxes(BindEntity.CurrentFrameIndex);
        if (hitBoxes != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.4f);
            foreach (var box in hitBoxes) DrawLogicBox(box);
        }
    }

    private void DrawLogicBox(LogicBox box)
    {
        float p2u = 0.01f;
        float realOffsetX = (BindEntity.IsFacingLeft ? -box.Center.x : box.Center.x) * p2u;
        float realOffsetY = box.Center.y * p2u;
        Vector3 worldCenter = new Vector3(BindEntity.pos.x + realOffsetX, BindEntity.pos.y + realOffsetY + BindEntity.height * p2u, 0);
        Vector3 size = new Vector3(box.Size.x * p2u, box.Size.y * p2u, 0.1f);
        Gizmos.DrawCube(worldCenter, size);
        Gizmos.DrawWireCube(worldCenter, size);
    }
}
