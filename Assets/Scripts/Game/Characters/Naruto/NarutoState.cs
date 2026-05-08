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
    private int _segmentTick = 0;   // 当前段落经历的逻辑帧数

    public override void Enter(CharacterEntity owner)
    {
        _comboIdx = 1;
        StartComboSegment(owner);
    }

    private void StartComboSegment(CharacterEntity owner)
    {
        _hasInputNext = false;
        _segmentTick = 0;

        // 1. 转向逻辑：每段攻击开始时，如果摇杆有输入则可以转向
        var input = owner.CurrInput;
        if (input != null && input.JoyStickAngle != 255)
        {
            float radians = input.JoyStickAngle * Mathf.Deg2Rad;
            float dx = Mathf.Cos(radians);
            if (Mathf.Abs(dx) > 0.1f) owner.IsFacingLeft = dx < 0;
        }

        // 2. 切换动画
        owner.SwitchAnimation($"Attack_{_comboIdx}");

        // 3. 初始化位移：普攻通常带有向前的位移
        float speed = GetMoveSpeed(_comboIdx);
        float dir = owner.IsFacingLeft ? -1f : 1f;
        owner.velocity = new Vector2(speed * dir, 0);

        Debug.Log($"[Battle] Naruto Attack_{_comboIdx} Enter, Speed: {speed}");
    }

    public override void Update(CharacterEntity owner)
    {
        _segmentTick++;
        var input = owner.CurrInput;

        // 1. 连招预输入检测
        if (input != null && (input.Buttons & ButtonMask.Attack) != 0)
        {
            _hasInputNext = true;
        }

        // 2. 位移衰减逻辑：攻击开始后的前几个 Tick 有位移，之后由于摩擦力或惯性停止
        // 这里的 4 个 Tick 约等于 0.26秒 (15fps)
        if (_segmentTick > 4)
        {
            owner.velocity = Vector2.zero;
        }

        // 3. 检查当前动画段落是否已经播放完毕
        if (IsAnimFinished(owner))
        {
            if (_hasInputNext && _comboIdx < 4)
            {
                _comboIdx++;
                StartComboSegment(owner);
            }
            else
            {
                // 连招结束
                owner.RootSM.ChangeState(CommonState.Idle);
            }
        }
    }

    private float GetMoveSpeed(int combo)
    {
        switch (combo)
        {
            case 1: return 4.0f;
            case 2: return 3.0f;
            case 3: return 5.0f;
            case 4: return 8.0f; // 最后一击前冲最远
            default: return 0;
        }
    }

    public override void Exit(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
    }

    public override HitData GetHitData(CharacterEntity owner)
    {
        // 动态根据当前的攻击段数构造 HitData
        HitData data = new HitData(HitType.Normal);
        data.Owner = owner;
        data.Player = owner;
        data.Pos = owner.pos;
        data.Height = owner.height;

        // 段数越高，伤害和硬直越高
        data.Damage = 10 + _comboIdx * 5;
        data.HitStun = 12 + _comboIdx * 2;

        // 最后一击具有击飞效果
        if (_comboIdx == 4)
        {
            data.HType = HitType.ToAir;
            data.HitStun = 40; // 击飞硬直更长
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
        // 逻辑层判断：当前已经是最后一帧，且 Tick 计数达到 Duration
        if (owner.CurrentFrameIndex >= steps.Count - 1)
        {
            var lastStep = steps[owner.CurrentFrameIndex];
            // 注意：LogicEntity 中 TickCounter 每次增加 RENDER_LOGIC_RATIO (2)
            if (owner.TickCounter >= lastStep.Duration - 2)
            {
                return true;
            }
        }
        return false;
    }
}

#endregion
