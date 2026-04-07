using KiHan.Logic;
using System;
using UnityEngine;

public class LogicEntity
{
    public Vector2 LogicPos; // 位置
    public float MoveSpeed = 5.0f; // 移动速度
    public const float LOGIC_TICK_TIME = 0.066f; // 逻辑帧间隔

    // 动画数据
    public AnimationFrameData IdleAnim;
    public AnimationFrameData RunAnim;

    public AnimationFrameData CurrentAnim; // 当前动画
    public int CurrentFrameIndex = 0; // 当前帧

    private int _tickCounter = 0; // 帧计数器
    public bool IsFacingLeft = false; // 朝向

    public void Tick(InputFrame input)
    {
        Debug.Log($"[Enity] player tick");

        if (input.JoyStickAngle == 255)
        {
            SwitchAnimation(IdleAnim);
        }
        else
        {
            ProcessMovement(input);
            SwitchAnimation(RunAnim);
        }

        UpdateAnimation();
    }

    private void ProcessMovement(InputFrame input)
    {
        // 映射 0-180 -> 0-360
        float degrees = input.JoyStickAngle * 2.0f;
        float radians = degrees * Mathf.Deg2Rad;

        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);

        float verticalMod = 0.7f;

        LogicPos.x += dx * MoveSpeed * LOGIC_TICK_TIME;
        LogicPos.y += dy * (MoveSpeed * verticalMod) * LOGIC_TICK_TIME;

        if (Mathf.Abs(dx) > 0.1f)
        {
            IsFacingLeft = dx < 0;
        }
    }
    public void SwitchAnimation(AnimationFrameData newAnim)
    {
        if (newAnim == null || CurrentAnim == newAnim) return;

        CurrentAnim = newAnim;
        CurrentFrameIndex = 0;
        _tickCounter = 0;
    }

    public void UpdateAnimation()
    {
        if (CurrentAnim == null || CurrentAnim.Frames.Count == 0) return;

        var frameData = CurrentAnim.Frames[CurrentFrameIndex];

        _tickCounter++;

        if (_tickCounter >= frameData.Duration)
        {
            _tickCounter = 0;

            if (CurrentFrameIndex < CurrentAnim.Frames.Count - 1)
            {
                CurrentFrameIndex++;
                ApplyRootMotion(CurrentAnim.Frames[CurrentFrameIndex].RootMotion);
            }
            else
            {
                if (CurrentAnim.IsLoop)
                {
                    CurrentFrameIndex = 0;
                }
            }
        }
    }

    private void ApplyRootMotion(Vector2 motion)
    {
        if (motion == Vector2.zero) return;

        float direction = IsFacingLeft ? -1f : 1f;
        LogicPos.x += motion.x * direction;
        LogicPos.y += motion.y;
    }

    public Sprite GetCurrentSprite()
    {
        if (CurrentAnim == null || CurrentFrameIndex >= CurrentAnim.Frames.Count) return null;
        return CurrentAnim.Frames[CurrentFrameIndex].Sprite;
    }
}