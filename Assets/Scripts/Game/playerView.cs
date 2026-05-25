using UnityEngine;
using KiHan.Logic;
using View;

/// <summary>
/// 玩家专属表现层
/// 继承自 EntityView，额外处理脚下阴影、调试判定盒等专属逻辑
/// </summary>
public class PlayerView : EntityView
{
    private GameObject _shadowGo;
    private SpriteRenderer[] _shadowSrs;
    private int[] _shadowBaseOrders;

    protected override void LateUpdate()
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
                    } 
                    else
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

        // 调用父类的核心更新逻辑 (位移、动画推进、渲染)
        base.LateUpdate();
    }

    protected override void OnRenderComplete(int baseOrder)
    {
        // 每次渲染完毕后，同步更新阴影层级
        UpdateShadows(baseOrder);
    }

    private void UpdateShadows(int baseOrder)
    {
        if (_shadowSrs != null)
        {
            for (int i = 0; i < _shadowSrs.Length; i++)
            {
                // 阴影整体放在人物下方(-20)，同时保留预制体原本的相对层级
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
