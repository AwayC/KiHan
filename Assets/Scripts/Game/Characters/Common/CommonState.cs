using KiHan.Logic;
using UnityEngine;
using System.Collections.Generic;
using System;

// 定义通用状态索引
public static class CommonState
{
    public const sbyte Idle = 0;
    public const sbyte Run = 1;
    public const sbyte Attack = 2;
    public const sbyte Hurt = 4;
    public const sbyte Land = 6;
    public const sbyte Skill = 7;
}

#region 通用基础状态类
public class CommonIdleState : StateBase
{
    public override sbyte StateType => CommonState.Idle;
    private ButtonMask _lastButtons = ButtonMask.None;

    public override void Enter(CharacterEntity owner) 
    {
        owner.ForceLoop = true; // Idle 也是循环的
        owner.SwitchAnimation("Idle");
        // 变成 Idle 的时候速度归零
        owner.velocity = Vector2.zero;
        _lastButtons = owner.CurrInput?.Buttons ?? ButtonMask.None;
    }
    public override void Exit(CharacterEntity owner) { }
    public override void Update(CharacterEntity owner)
    {
        owner.velocity = Vector2.zero; // 确保静止

        var input = owner.CurrInput;
        if (input == null) return;

        // 恢复为简单的状态检查：只要按着攻击键就进入攻击状态
        if ((input.Buttons & ButtonMask.Attack) != 0) {
            owner.RootSM.ChangeState(CommonState.Attack);
            return;
        }

        if (input.JoyStickAngle != 255) {
            owner.RootSM.ChangeState(CommonState.Run);
        }
    }
}

public class CommonRunState : StateBase
{
    public override sbyte StateType => CommonState.Run;

    public override void Enter(CharacterEntity owner) 
    {
        owner.ForceLoop = true; // 强制 Run 循环播放
        owner.SwitchAnimation("Run");

        // 立即更新一次朝向，处理短按情况
        var input = owner.CurrInput;
        if (input != null && input.JoyStickAngle != 255)
        {
            float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
            float dx = Mathf.Cos(radians);
            if (Mathf.Abs(dx) > 0.3f) owner.IsFacingLeft = dx < 0;
        }
    }
    public override void Exit(CharacterEntity owner) 
    {
        owner.ForceLoop = false;
        owner.velocity = Vector2.zero;

        Debug.Log("Run" + owner.pos);
    }
    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;
        if (input == null || input.JoyStickAngle == 255) { 
            owner.RootSM.ChangeState(CommonState.Idle); 
            return; 
        }
        
        if ((input.Buttons & ButtonMask.Attack) != 0) { 
            owner.RootSM.ChangeState(CommonState.Attack); 
            return; 
        }

        float radians = input.JoyStickAngle * 2.0f * Mathf.Deg2Rad;
        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);
        
        owner.velocity = new Vector2(dx * owner.MoveSpeed, dy * owner.MoveSpeed * 0.6f);
        
        // 更新朝向 (增加防抖死区)
        if (Mathf.Abs(dx) > 0.3f) owner.IsFacingLeft = dx < 0;
    }
}

public class CommonHurtState : StateBase
{
    public override sbyte StateType => CommonState.Hurt;

    private int anim_idx = 1;
    private string _currentHurtAnim = "";

    public override void Enter(CharacterEntity owner)
    {
        // 1. 初始化击退速度
        if (owner.LastHitData != null)
        {
            float pushDir = owner.LastHitData.PushDirX;
            if (pushDir == 0) pushDir = (owner.pos.x >= owner.LastHitData.Pos.x) ? 1.0f : -1f;

            bool alreadyInAir = owner.IsAirborne;

            if (!alreadyInAir)
                owner.velocity = new Vector2(pushDir * owner.LastHitData.PushSpeed, 0);
            else
                owner.velocity = new Vector2(pushDir * owner.LastHitData.PushSpeedAir, 0);
            // 不用转向
            //owner.IsFacingLeft = (pushDir > 0);

            // --- 核心：浮空与连段追击 (Juggle) 判定 ---
            // 依赖 CharacterEntity 中保存的状态，解决边界高度判定失效的问题
            

            if (owner.LastHitData.HType == HitType.ToAir)
            {
                // A. 强力击飞
                owner.h_vel = owner.LastHitData.PushSpeedY; 
                owner.Gravity = 5; // 击飞时重力变小，增加浮空时间
                _currentHurtAnim = "Hurt_toair";
                owner.SwitchAnimation(_currentHurtAnim, true);
                owner.ForceNotLoop = true;
            }
            else if (alreadyInAir)
            {
                // B. 空中追击 (Juggle)
                owner.h_vel = owner.LastHitData.PushSpeedY; 
                owner.Gravity = 5; // 追击时同样保持小重力
                _currentHurtAnim = "Hurt_inair";
                owner.SwitchAnimation(_currentHurtAnim, true);
                owner.ForceNotLoop = true;
            }
            else
            {
                // C. 地面普通受击：即便攻击包带了垂直速度，只要不是 ToAir 且目标在地面，就不产生离地效果
                owner.h_vel = 0;
                owner.Gravity = 10; // 恢复正常重力

                _currentHurtAnim = $"Hurt_{anim_idx}";
                owner.SwitchAnimation(_currentHurtAnim, true);
                anim_idx = anim_idx % 4 + 1;
                owner.ForceNotLoop = true;
            }
        }
    }

    public override void Exit(CharacterEntity owner) 
    {
        owner.velocity = Vector2.zero;
        owner.ForceLoop = false;
        owner.Gravity = 10; // 退出受击状态时恢复默认重力
        _currentHurtAnim = "";
    }

    public override void Update(CharacterEntity owner)
    {
        // --- 2. 击退速度衰减 ---
        bool isAerialHurt = (_currentHurtAnim == "Hurt_toair" || _currentHurtAnim == "Hurt_inair" || _currentHurtAnim == "Hurt_fall");

        if (isAerialHurt)
        {
            // 核心修复：空中完全移除水平摩擦力。
            // 只有这样，角色在下落过程中才会保持水平惯性，形成完整的抛物线，而不是垂直下落。
            // owner.velocity 保持不变
        }
        else
        {
            // 地面摩擦力大，保持短促有力的手感/
            owner.velocity *= 0.5f;
            if (owner.velocity.magnitude < 0.1f) owner.velocity = Vector2.zero;
        }

        // --- 3. 浮空逻辑处理 ---

        if (isAerialHurt)
        {
            // 落地判定：必须高度归零且速度向下或为零
            if (owner.height <= 0 && owner.h_vel <= 0)
            {
                owner.IsAirborne = false; // 清除浮空状态
                owner.RootSM.ChangeState(CommonState.Land);
                return;
            }

            // 动画阶段迁移：当 Hurt_toair 或 Hurt_inair 播完，自动切到下落 Hurt_fall
            if (_currentHurtAnim != "Hurt_fall" && IsAnimFinished(owner))
            {
                _currentHurtAnim = "Hurt_fall";
                owner.SwitchAnimation(_currentHurtAnim, true);
                owner.ForceNotLoop = false; // 下落动作允许循环
                owner.ForceLoop = true;
            }

            return; // 只要在天上，就无视地面硬直时间
        }

        // --- 4. 地面受击硬直计时器优先：只要还有时间，就强制停在受击状态
        if (owner.StunTimer > 0)
        {
            owner.StunTimer--;
            return;
        }

        owner.RootSM.ChangeState(CommonState.Idle);
    }

    private bool IsAnimFinished(CharacterEntity owner)
    {
        if (owner.CurrAnim == null) return true;
        var steps = owner.CurrAnim.Steps;
        int frameIdx = Mathf.Clamp(owner.CurrentFrameIndex, 0, steps.Count - 1);

        if (frameIdx >= steps.Count - 1)
        {
            var lastStep = steps[frameIdx];
            if (owner.LogicalTickCounter >= lastStep.Duration)
                return true;
        }
        return false;
    }
}

public class CommonLandState : StateBase
{
    public override sbyte StateType => CommonState.Land;

    public override void Enter(CharacterEntity owner)
    {
        owner.IsAirborne = false; // 确保清空浮空状态
        
        // 判定：如果上一个状态是受击(Hurt)，播放 Hurt_land；否则播普通 Land
        if (owner.RootSM.LastState != null && owner.RootSM.LastState.StateType == CommonState.Hurt)
        {
            owner.SwitchAnimation("Hurt_land");
        }
        else
        {
            owner.SwitchAnimation("Land");
        }

        owner.velocity = Vector2.zero;
    }

    public override void Exit(CharacterEntity owner) { }

    public override void Update(CharacterEntity owner)
    {
        var input = owner.CurrInput;
        // 允许在落地收招期间通过攻击键取消，进入下一轮 1a
        if (input != null && (input.Buttons & ButtonMask.Attack) != 0)
        {
            owner.RootSM.ChangeState(CommonState.Attack);
            return;
        }

        // 动画播放完毕后进入 Idle
        if (IsAnimFinished(owner))
        {
            owner.RootSM.ChangeState(CommonState.Idle);
        }
    }

    private bool IsAnimFinished(CharacterEntity owner)
    {
        if (owner.CurrAnim == null) return true;
        var steps = owner.CurrAnim.Steps;
        int frameIdx = Mathf.Clamp(owner.CurrentFrameIndex, 0, steps.Count - 1);

        if (frameIdx >= steps.Count - 1)
        {
            var lastStep = steps[frameIdx];
            if (owner.LogicalTickCounter >= lastStep.Duration)
                return true;
        }
        return false;
    }
}

#endregion
