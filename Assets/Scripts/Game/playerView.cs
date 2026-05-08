using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;

public class PlayerView : MonoBehaviour
{
    public LogicEntity BindEntity;
    public float SmoothSpeed = 13f; 
    
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

        // 1. 位置平滑 (容器追踪逻辑坐标，逻辑坐标已经是 Unity 单位了，不需要乘 p2u)
        Vector3 targetPos = new Vector3(BindEntity.pos.x, BindEntity.pos.y, 0);
        
        if (Vector3.Distance(transform.position, targetPos) < 0.001f)
            transform.position = targetPos;
        else
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * SmoothSpeed);

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
        _visualFrameIndex = 0;
        return;
    }

    // 2. 移除冗余的重置判定（因为有了 Version）
    
    // 3. 自驱动推进：无论逻辑层是否 Tick，表现层都按 30fps 推进
    _visualTimer += Time.deltaTime;
    float renderTickTime = GameConfig.RENDER_TICK_TIME;

    bool isLooping = currentAnim.IsLoop || BindEntity.ForceLoop;
    
    // 安全检查
    if (_visualFrameIndex >= currentAnim.Steps.Count) _visualFrameIndex = 0;
    
    var step = currentAnim.Steps[_visualFrameIndex];
    if (_visualTimer >= step.Duration * renderTickTime)
    {
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
            // 非循环动画停在最后一帧
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
        float logicHeight = BindEntity.height * p2u; 

        // 动态计算渲染层级：
        // 1. (2000 - Round(y*100)) 决定大层级，基数设为 2000 防止 Int16 溢出
        // 2. 乘以 2 为每个 Y 轴坐标点留出槽位
        // 3. 加上 owner ID 确保层级唯一性
        int baseOrder = (2000 - Mathf.RoundToInt(BindEntity.pos.y * 100f)) * 2 + BindEntity.owner;
        _mainSr.sortingOrder = baseOrder;

        // A. 渲染本体 (只修改子节点的 localPosition)
        _mainSr.sprite = frameData.Sprite;
        _mainSr.flipX = BindEntity.IsFacingLeft;
        
        float offX = (_mainSr.flipX ? -frameData.Offset.x : frameData.Offset.x) * p2u;
        float offY = frameData.Offset.y * p2u;
        
        _displayRoot.localPosition = new Vector3(offX, offY + logicHeight, 0);

        // B. 渲染特效图层
        int layerCount = frameData.ExtraLayers.Count;
        while (_extraSrs.Count < layerCount)
        {
            GameObject go = new GameObject($"Layer_{_extraSrs.Count}");
            go.transform.SetParent(_displayRoot); // 挂在 Display 节点下
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
                
                // 特效的偏移相对于 Display 节点
                float lx = (_mainSr.flipX ? -layer.Offset.x : layer.Offset.x) * p2u;
                float ly = layer.Offset.y * p2u;
                sr.transform.localPosition = new Vector3(lx, ly, 0);
            }
            else sr.gameObject.SetActive(false);
        }

        // C. 更新脚下阴影图层的动态层级
        if (_shadowSrs != null)
        {
            for (int i = 0; i < _shadowSrs.Length; i++)
            {
                // 阴影整体放在人物下方(-20)，同时保留预制体原本的相对层级
                _shadowSrs[i].sortingOrder = baseOrder - 20 + _shadowBaseOrders[i];
            }
        }
    }
}
