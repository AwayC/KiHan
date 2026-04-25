using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;

// 鸣人特有的状态放在这里
#region 战斗状态 (Attack / Skill)

public class NarutoStateAttack : StateBase
{
    public override sbyte StateType => CommonState.Attack;

    private int _comboIdx = 1;      // 当前连击段数 (1-4)
    private bool _hasInputNext = false; // 是否有预输入

    public override void Enter(CharacterEntity owner)
    {
        _comboIdx = 1;
        _hasInputNext = false;
        owner.SwitchAnimation($"Attack_{_comboIdx}");
        Debug.Log($"[Battle] Naruto starts Attack {_comboIdx}");
    }

    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;

        // 1. 连招预输入检测：在当前动作播放期间按下攻击键，记录标识
        if (input != null && (input.Buttons & ButtonMask.Attack) != 0)
        {
            _hasInputNext = true;
        }

        // 2. 检查当前动画段落是否已经播放完毕
        if (IsAnimFinished(owner))
        {
            // 如果有预输入且未到最后一击，进入下一段
            if (_hasInputNext && _comboIdx < 4)
            {
                _comboIdx++;
                _hasInputNext = false;
                owner.SwitchAnimation($"Attack_{_comboIdx}");
                Debug.Log($"[Battle] Naruto combo to Attack {_comboIdx}");
            }
            else
            {
                // 否则连招结束，回到待机
                owner.RootSM.ChangeState(CommonState.Idle);
            }
        }
    }

    public override void Exit(CharacterEntity owner)
    {
        _comboIdx = 1;
        _hasInputNext = false;
    }

    public override HitData GetHitData(CharacterEntity owner)
    {
        // 动态根据当前的攻击段数构造 HitData
        HitData data = new HitData(HitType.Normal);
        data.Damage = 10 * _comboIdx; // 越往后伤害越高
        data.HitStun = 15 + _comboIdx * 2;
        data.Owner = owner;
        data.Player = owner;
        data.Pos = owner.pos;
        data.Height = owner.height;

        // 第 4 段平 A 增加击飞效果
        if (_comboIdx == 4)
        {
            data.HType = HitType.ToAir;
        }

        return data;
    }

    /// <summary>
    /// 辅助方法：判断当前动画序列是否执行完毕
    /// </summary>
    private bool IsAnimFinished(CharacterEntity owner)
    {
        if (owner.CurrAnim == null) return true;
        
        var steps = owner.CurrAnim.Steps;
        // 已经到最后一步，且当前步的 Tick 计数已满
        if (owner.CurrentFrameIndex >= steps.Count - 1)
        {
            if (owner.TickCounter >= steps[owner.CurrentFrameIndex].Duration - 1)
            {
                return true;
            }
        }
        return false;
    }
}

#endregion
