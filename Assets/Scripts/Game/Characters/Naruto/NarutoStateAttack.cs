using KiHan.Logic;
using UnityEngine;


// 鸣人特有的状态放在这里
#region 战斗状态 (Attack / Skill)

public class NarutoStateAttack : StateBase
{
    public override sbyte StateType => CommonState.Attack;

    private int _comboIdx = 1;      // 当前连击段数 (1-4)
    private bool _hasInputNext = false; // 是否有有效的预输入
    private ButtonMask _lastButtons = ButtonMask.None; // 上一帧的按键状态

    public override void Enter(CharacterEntity owner)
    {
        _comboIdx = 1;
        owner.velocity = Vector2.zero;
        owner.ForceLoop = false; 
        _lastButtons = owner.CurrInput != null ? owner.CurrInput.Buttons : ButtonMask.None;
        StartComboSegment(owner);
    }

    private void StartComboSegment(CharacterEntity owner)
    {
        _hasInputNext = false;

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

        // 3. 初始速度清零，移动完全交给 FrameData 里的 RootMotion
        owner.velocity = Vector2.zero;

        Debug.Log($"[Battle] Naruto Attack_{_comboIdx} Enter.");
    }

    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;
        if (input != null)
        {
            // 允许长按连招
            bool isDown = (input.Buttons & ButtonMask.Attack) != 0;
            if (isDown && CheckInputWindow(owner))
            {
                _hasInputNext = true;
            }
            _lastButtons = input.Buttons;
        }

        // --- 1. Attack 4 特殊逻辑：空中等待落地 ---
        // 纯逻辑层控制：如果在空中，强制锁定在动画的最后一帧
        if (_comboIdx == 4)
        {
            int lastFrame = (owner.CurrAnim != null) ? owner.CurrAnim.Steps.Count - 1 : 9;

            if (owner.height > 0)
            {
                if (owner.CurrentFrameIndex >= lastFrame)
                {
                    owner.CurrentFrameIndex = lastFrame;
                    owner.LogicalTickCounter = 0; // 冻结计时器，永远不让它在空中自然结束
                }
            }
            else
            {
                // 落地瞬间：只要已经过了起跳帧（约第5帧），一旦触地立刻结束普攻状态
                if (owner.CurrentFrameIndex >= 7)
                {
                    owner.velocity = Vector2.zero;

                    if (_hasInputNext)
                    {
                        // 循环化连招
                        _comboIdx = 1; 
                        StartComboSegment(owner);
                    }
                    else
                    {
                        // 切入落地收招状态 (Land)
                        owner.RootSM.ChangeState(CommonState.Land);
                    }
                    return; // 立即返回，不走底部的 IsAnimFinished
                }
            }
        }

        // 3. 检查当前动画段落是否已经播放完毕
        if (owner.IsAnimEnd())
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
                // 如果是 4a 结束且没有预输入（作为异常情况的兜底），切入落地收招状态
                if (_comboIdx == 4)
                {
                    owner.RootSM.ChangeState(CommonState.Land);
                }
                else
                {
                    // 1a-3a 结束则直接回待机
                    owner.RootSM.ChangeState(CommonState.Idle);
                }
            }
        } 
        else if (_hasInputNext)
        {
            if(_comboIdx <= 3)
            {
                _comboIdx++;
                if (_comboIdx > 4) _comboIdx = 1;
                StartComboSegment(owner);
            }
        }

        switch (_comboIdx)
        {
            case 1:
                if (owner.CurrentFrameIndex == 0 && owner.LogicalTickCounter == 2)
                {
                    owner.SetHitData(GetHitData(owner));
                }
                break;
            case 2:
                if (owner.CurrentFrameIndex == 0 && owner.LogicalTickCounter == 0)
                {
                    owner.SetHitData(GetHitData(owner));
                }
                break;
            case 3: 
                if(owner.CurrentFrameIndex == 0 && owner.LogicalTickCounter == 0)
                {
                    owner.SetHitData(GetHitData(owner));
                }
                break;
            case 4:
                if (owner.CurrentFrameIndex == 0 && owner.LogicalTickCounter == 0)
                {
                    owner.SetHitData(GetHitData(owner));
                }

                if (owner.CurrentFrameIndex == 5 && owner.LogicalTickCounter == 0)
                {
                    owner.SetHitData(GetHitData(owner));
                }

                break;
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
            // 对于 4a，它会在空中被冻结在最后一帧。
            // 允许在最后一帧（下落过程）及其之后进行预输入，这样一落地就能无缝连招。
            return currentFrame >= (totalFrames - 1);
        }
    }

    public override void Exit(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero;
    }

    public HitData GetHitData(CharacterEntity owner)
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
        data.PushSpeedAir = 2f; // 空中追击时的水平击退（稍小一点防止打飞太远接不上）

        // 默认垂直速度：为了支持空中追击 (Juggle)，普通攻击也带一点点向上力
        data.PushSpeedY = 30;

        // 配置打击特效
        data.HitEffectName = "HitSpark_Normal";
        data.HitEffectOffset = new Vector2(50f, 60f); // 相对受击者的偏移 (向着受击者前方)

        if (_comboIdx == 4 && owner.CurrentFrameIndex >= 4)
        {
            data.HType = HitType.ToAir;
            data.HitStun = 40; 
            data.PushSpeed = 30f; 
            data.PushSpeedAir = 3f; 
            data.PushSpeedY = 55; // 4a 击飞更高
            data.IsHeavyHit = true; // 触发重击连震
            
            // 最后一击替换为重击特效
            data.HitEffectName = "HitSpark_Heavy";
            data.HitEffectOffset = new Vector2(60f, 80f);
        }
        return data;
    }

    private bool IsAnimFinished(CharacterEntity owner)
    {
        if (owner.CurrAnim == null) return true;
        var steps = owner.CurrAnim.Steps;
        
        // 边界保护
        int frameIdx = Mathf.Clamp(owner.CurrentFrameIndex, 0, steps.Count - 1);
        
        if (frameIdx >= steps.Count - 1)
        {
            var lastStep = steps[frameIdx];
            // 使用严格大于等于时长判定
            if (owner.LogicalTickCounter >= lastStep.Duration)
            {
                return true;
            }
        }
        return false;
    }
}

#endregion
