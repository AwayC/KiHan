using KiHan.Logic;
using System;
using UnityEngine;

public class LogicEntity
{
    // --- 核心物理/逻辑数据 ---
    public Vector2 LogicPos;
    public float MoveSpeed = 5.0f;

    // 逻辑帧间隔：15 FPS = 0.066s (如果未来改为 60 FPS 则设为 0.016s)
    public const float LOGIC_TICK_TIME = 0.066f;

    // --- 动画数据引用 ---
    public AnimationFrameData IdleAnim;
    public AnimationFrameData RunAnim;

    // --- 当前运行状态 ---
    public AnimationFrameData CurrentAnim;
    public int CurrentFrameIndex = 0;

    // 核心计数器：记录当前图片已经在逻辑层停留了几个 Tick
    private int _tickCounter = 0;

    // 方向控制 (true 为向左，false 为向右)
    public bool IsFacingLeft = false;

    /// <summary>
    /// 每一帧逻辑更新
    /// </summary>
    public void Tick(InputFrame input)
    {
        Debug.Log($"[Enity] player tick");
        // 1. 处理输入与移动逻辑
        if (input.JoyStickAngle == 255)
        {
            // 摇杆回正：切换到待机
            SwitchAnimation(IdleAnim);
        }
        else
        {
            // 有摇杆输入：处理 2.5D 位移
            ProcessMovement(input);
            // 切换到跑动
            SwitchAnimation(RunAnim);
        }

        // 2. 驱动动画帧进阶（基于 Duration 判定）
        UpdateAnimation();
    }

    /// <summary>
    /// 处理移动与朝向
    /// </summary>
    private void ProcessMovement(InputFrame input)
    {
        // 映射角度 (0-255 -> 0-360)
        float degrees = input.JoyStickAngle * 2.0f;
        float radians = degrees * Mathf.Deg2Rad;

        float dx = Mathf.Cos(radians);
        float dy = Mathf.Sin(radians);

        // 纵向速度修正 (2.5D 透视感)
        float verticalMod = 0.7f;

        // 基础位移计算
        LogicPos.x += dx * MoveSpeed * LOGIC_TICK_TIME;
        LogicPos.y += dy * (MoveSpeed * verticalMod) * LOGIC_TICK_TIME;

        // 更新朝向
        if (Mathf.Abs(dx) > 0.1f)
        {
            IsFacingLeft = dx < 0;
        }
    }

    /// <summary>
    /// 切换动画状态
    /// </summary>
    public void SwitchAnimation(AnimationFrameData newAnim)
    {
        if (newAnim == null || CurrentAnim == newAnim) return;

        CurrentAnim = newAnim;
        CurrentFrameIndex = 0;
        _tickCounter = 0; // 切换动画时重置计数器
    }

    /// <summary>
    /// 核心逻辑：基于 Duration（持续帧数）更新动画
    /// </summary>
    public void UpdateAnimation()
    {
        if (CurrentAnim == null || CurrentAnim.Frames.Count == 0) return;

        // 获取当前帧的配置数据
        var frameData = CurrentAnim.Frames[CurrentFrameIndex];

        // 逻辑计数自增 (每个 Tick +1)
        _tickCounter++;

        // 判定：当前图片停留的 Tick 是否达到了配置的时长？
        if (_tickCounter >= frameData.Duration)
        {
            _tickCounter = 0; // 重置计数器

            // 检查是否还有下一帧
            if (CurrentFrameIndex < CurrentAnim.Frames.Count - 1)
            {
                CurrentFrameIndex++;

                // 【重点】当帧发生切换时，应用该帧带有的根位移 (RootMotion)
                ApplyRootMotion(CurrentAnim.Frames[CurrentFrameIndex].RootMotion);
            }
            else
            {
                // 动画结束后的处理
                if (CurrentAnim.IsLoop)
                {
                    CurrentFrameIndex = 0;
                }
                // 如果不循环，则会一直停留在最后一帧
            }
        }
    }

    /// <summary>
    /// 应用动画带来的逻辑位移（解决脚滑）
    /// </summary>
    private void ApplyRootMotion(Vector2 motion)
    {
        if (motion == Vector2.zero) return;

        // 根位移也要根据朝向进行翻转
        float direction = IsFacingLeft ? -1f : 1f;
        LogicPos.x += motion.x * direction;
        LogicPos.y += motion.y;
    }

    /// <summary>
    /// 供表现层获取当前显示的图片
    /// </summary>
    public Sprite GetCurrentSprite()
    {
        if (CurrentAnim == null || CurrentFrameIndex >= CurrentAnim.Frames.Count) return null;
        return CurrentAnim.Frames[CurrentFrameIndex].Sprite;
    }
}