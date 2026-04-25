using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;

public class PlayerView : MonoBehaviour
{
    public LogicEntity BindEntity;
    public float SmoothSpeed = 30f; 
    
    private SpriteRenderer _mainSr;
    private Transform _displayRoot; // 专门负责美术偏移的节点
    private List<SpriteRenderer> _extraSrs = new List<SpriteRenderer>();

    private AnimationFrameData _lastAnim;
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
        
        // 设置层级，确保在背景（通常为 0 或负数）之上
        _mainSr.sortingOrder = 10;
    }

    private void LateUpdate()
    {
        if (BindEntity == null) return;
        
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

    // 只有当动画资源对象发生变化时，才重置计时器
    if (currentAnim != _lastAnim)
    {
        _lastAnim = currentAnim;
        _visualTimer = 0f;
        _visualFrameIndex = 0;
        return;
    }

    // 如果逻辑层开启了强制循环且资源本身没标记循环，或者非循环动画
    // 此时必须硬同步逻辑层的帧索引，以保证逻辑判定盒(Logic)和视觉(View)严格对齐
    if (!currentAnim.IsLoop)
    {
        _visualFrameIndex = BindEntity.CurrentFrameIndex;
        return;
    }

    // 资源本身标记了循环，则使用表现层平滑插值
    _visualTimer += Time.deltaTime;
    float tickTime = LogicEntity.LOGIC_TICK_TIME;

    if (_visualFrameIndex >= currentAnim.Steps.Count) _visualFrameIndex = 0;

    var step = currentAnim.Steps[_visualFrameIndex];
    if (_visualTimer >= step.Duration * tickTime)
    {
        _visualTimer = 0;
        _visualFrameIndex = (_visualFrameIndex + 1) % currentAnim.Steps.Count;
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
    }
}
