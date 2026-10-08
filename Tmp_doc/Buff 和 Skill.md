## Buff 和 Skill





``` c#
public class BuffAConfig
{
    public string ID;
    public string BuffName;
    public string BuffDesic;
    public string Icon;
    public string BuffEffectName;
    public float BuffDefense;
    public float BuffAttack;
    public float BuffFrezzeTime;
    public float BuffTime;
    public string Cost;
}
```

框架

1. 行为层：处理各种战斗单元操作事件
    玩家输入操作（UI，手柄）----> 不直接调用策略，通过发送事件，统一处理
    NPC/AI
    网络玩家输入

2.  策略层：提供各种处理的逻辑策略
    FightMgr.ProcessEvent(操作，玩家{ 

      玩家发动技能
    }
    GM_Charactor--> 通用战斗单元

3. 功能组件层：提供功能机制，不提供策略
    行走组件，动画播放，攻击组件
    技能，buff，玩家数据，动画切换
