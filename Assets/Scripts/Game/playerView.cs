using UnityEngine;
using KiHan.Logic;
using System.Collections.Generic;

public class PlayerView : MonoBehaviour
{
    public LogicEntity BindEntity;
    public float SmoothSpeed = 15f;
    
    private SpriteRenderer _mainSr;
    private List<SpriteRenderer> _extraSrs = new List<SpriteRenderer>();

    private AnimationFrameData _lastAnim;
    private float _visualTimer = 0f;
    private int _visualFrameIndex = 0;

    private void Awake()
    {
        _mainSr = GetComponent<SpriteRenderer>();
        if (_mainSr == null) _mainSr = gameObject.AddComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (BindEntity == null) return;
        // 1. 位置平滑
        Vector3 targetPos = new Vector3(BindEntity.LogicPos.x, BindEntity.LogicPos.y, 0);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * SmoothSpeed);

        // 2. 动画索引推进
        UpdateVisualIndex();

        // 3. 渲染
        RenderCurrent();
    }

    private void UpdateVisualIndex()
    {
        var currentAnim = BindEntity.CurrentAnim;
        if (currentAnim == null || currentAnim.Steps.Count == 0) return;

        if (currentAnim != _lastAnim)
        {
            _lastAnim = currentAnim;
            _visualTimer = 0f;
            _visualFrameIndex = 0;
        }

        if (currentAnim.IsLoop)
        {
            _visualTimer += Time.deltaTime;
            float tickTime = LogicEntity.LOGIC_TICK_TIME;
            var step = currentAnim.Steps[_visualFrameIndex];
            if (_visualTimer >= step.Duration * tickTime)
            {
                _visualTimer = 0;
                _visualFrameIndex = (_visualFrameIndex + 1) % currentAnim.Steps.Count;
            }
        }
        else
        {
            _visualFrameIndex = BindEntity.CurrentFrameIndex;
        }
    }

    private void RenderCurrent()
    {
        var currentAnim = BindEntity.CurrentAnim;
        if (currentAnim == null) return;

        // 获取当前步对应的帧数据
        var frameData = currentAnim.GetCurrentFrameData(_visualFrameIndex);
        if (frameData == null) return;

        // A. 渲染本体
        _mainSr.sprite = frameData.Sprite;
        _mainSr.flipX = BindEntity.IsFacingLeft;
        float offX = _mainSr.flipX ? -frameData.Offset.x : frameData.Offset.x;
        _mainSr.transform.localPosition = new Vector3(offX, frameData.Offset.y + BindEntity.LogicHeight, 0);

        // B. 渲染特效图层
        int layerCount = frameData.ExtraLayers.Count;
        while (_extraSrs.Count < layerCount)
        {
            GameObject go = new GameObject($"Layer_{_extraSrs.Count}");
            go.transform.SetParent(this.transform);
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
                
                float lx = _mainSr.flipX ? -(frameData.Offset.x + layer.Offset.x) : (frameData.Offset.x + layer.Offset.x);
                sr.transform.localPosition = new Vector3(lx, frameData.Offset.y + layer.Offset.y + BindEntity.LogicHeight, 0);
            }
            else sr.gameObject.SetActive(false);
        }
    }
}
