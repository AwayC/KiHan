using KiHan.Logic;
using UnityEngine;

/// <summary>
/// 技能派生实体 (如飞行道具、召唤物)
/// </summary>
public class SkillDerivedEntity : LogicEntity
{
    public Vector2 Velocity;
    public float LifeTime = 2.0f;
    private float _timer = 0;

    public override void Tick(InputFrame input)
    {
        LogicPos += Velocity * LOGIC_TICK_TIME;
        _timer += LOGIC_TICK_TIME;
        
        // 逻辑更新后调用基类处理动画
        base.Tick(input);
    }
}
