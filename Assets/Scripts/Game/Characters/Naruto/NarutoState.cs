using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;

// 鸣人特有的状态放在这里
#region 战斗状态 (Attack / Skill)

public class NarutoStateAttack : StateBase
{
    public override sbyte StateType => CommonState.Attack;

    private int _comboIdx = 1;      // 当前连击段数 (1-4)
    private bool _hasInputNext = false; // 是否有有效的预输入
    private int _segmentTick = 0;   // 当前段落经历的逻辑帧数
    private ButtonMask _lastButtons = ButtonMask.None; // 上一帧的按键状态

    public override void Enter(CharacterEntity owner)
    {
        _comboIdx = 1;
        _lastButtons = owner.CurrInput != null ? owner.CurrInput.Buttons : ButtonMask.None;
        StartComboSegment(owner);
    }

    private void StartComboSegment(CharacterEntity owner)
    {
        _hasInputNext = false;
        _segmentTick = 0;

        // 1. 转向逻辑
        var input = owner.CurrInput;
        if (input != null && input.JoyStickAngle != 255)
        {
            float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
            float dx = Mathf.Cos(radians);
            if (Mathf.Abs(dx) > 0.1f) owner.IsFacingLeft = dx < 0;
        }

        // 2. 切换动画
        owner.SwitchAnimation($"Attack_{_comboIdx}");

        Debug.Log($"[Battle] Naruto Attack_{_comboIdx} Enter.");
    }

    public override void Update(CharacterEntity owner)
    {
        _segmentTick++;
        var input = owner.CurrInput;

        if (input != null)
        {
            // 核心修改：允许长按 (isDown)
            // 只要按键处于按下状态，且当前进度在“有效取消区间”内，就自动记录连招
            bool isDown = (input.Buttons & ButtonMask.Attack) != 0;
            
            if (isDown && CheckInputWindow(owner))
            {
                _hasInputNext = true;
            }
            _lastButtons = input.Buttons;
        }

        // --- Attack 4 特殊逻辑：空中等待落地 ---
        if (_comboIdx == 4)
        {
            // 在第 8 帧滞留，直到落地
            if (owner.CurrentFrameIndex == 8 && owner.height > 0)
            {
                owner.FreezeAnimFrame = true;
            }
            else
            {
                owner.FreezeAnimFrame = false;
            }
        }

        // 3. 检查当前动画段落是否已经播放完毕
        if (IsAnimFinished(owner))
        {
            if (_hasInputNext)
            {
                // 循环化连招
                _comboIdx++;
                if (_comboIdx > 4) _comboIdx = 1; 

                StartComboSegment(owner);
            }
            else
            {
                // 如果没有有效的预输入，播放完毕后停在待机
                owner.RootSM.ChangeState(CommonState.Idle);
            }
        }
    }

    /// <summary>
    /// 精确判定当前是否属于可接收下一连招输入的“取消窗口”
    /// </summary>
    private bool CheckInputWindow(CharacterEntity owner)
    {
        if (owner.CurrAnim == null) return false;
        
        int currentFrame = owner.CurrentFrameIndex;
        int totalFrames = owner.CurrAnim.Steps.Count;

        if (_comboIdx < 4)
        {
            // 对于短促的 1a-3a，允许在最后 2 帧内进行预输入
            return currentFrame >= (totalFrames - 2);
        }
        else
        {
            // 对于漫长的 4a，必须在落地帧（第 9 帧）及其之后才允许输入
            // 这解决了在天上乱点也能接下一套的问题
            return currentFrame >= 9;
        }
    }

    public override void Exit(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
        owner.FreezeAnimFrame = false; // 退出状态时确保解冻
    }

    public override HitData GetHitData(CharacterEntity owner)
    {
        HitData data = new HitData(HitType.Normal);
        data.Owner = owner;
        data.Player = owner;
        data.Pos = owner.pos;
        data.Height = owner.height;
        data.PushDirX = owner.IsFacingLeft ? -1f : 1f;

        data.Damage = 10 + _comboIdx * 5;
        data.HitStun = 12 + _comboIdx * 2;
        data.PushSpeed = 10.0f + _comboIdx * 2f; 

        if (_comboIdx == 4)
        {
            data.HType = HitType.ToAir;
            data.HitStun = 40; 
            data.PushSpeed = 12.0f; 
        }
        return data;
    }

    private bool IsAnimFinished(CharacterEntity owner)
    {
        if (owner.CurrAnim == null) return true;
        var steps = owner.CurrAnim.Steps;
        if (owner.CurrentFrameIndex >= steps.Count - 1)
        {
            var lastStep = steps[owner.CurrentFrameIndex];
            if (owner.TickCounter >= lastStep.Duration - GameConfig.RENDER_LOGIC_RATIO)
            {
                return true;
            }
        }
        return false;
    }
}

#endregion
