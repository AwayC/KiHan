using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Logic
{
    public class FrameAnimator : MonoBehaviour
    {
        [Header("渲染节点绑定 (可选)")]
        [SerializeField] private SpriteRenderer mainRenderer;
        [SerializeField] private Transform displayRoot;

        // 动画资源字典: animKey -> AnimationFrameData
        private readonly Dictionary<string, AnimationFrameData> _animDict = new(StringComparer.OrdinalIgnoreCase);

        // ------------------ 整数帧运行时状态 ------------------
        /// <summary>
        /// 当前播放的动画标识名
        /// </summary>
        public string CurrentAnimKey { get; private set; } = string.Empty;

        /// <summary>
        /// 当前动画资源引用
        /// </summary>
        public AnimationFrameData CurrentAnimData { get; private set; }

        /// <summary>
        /// 当前动作已播放的整数帧数 (0 到 TotalFrames)
        /// </summary>
        public int ElapsedFrames { get; private set; } = 0;

        /// <summary>
        /// 当前动作的总逻辑帧数 (由各个 Step.Duration 累加得出)
        /// </summary>
        public int TotalFrames { get; private set; } = 0;

        /// <summary>
        /// 当前激活的切片步索引 (Step Index)
        /// </summary>
        public int CurrentStepIndex { get; private set; } = 0;

        /// <summary>
        /// 当前切片步在内部流逝的帧数
        /// </summary>
        public int CurrentStepElapsedTicks { get; private set; } = 0;

        /// <summary>
        /// 当前动作是否播放完毕
        /// </summary>
        public bool IsFinished => ElapsedFrames >= TotalFrames;

        /// <summary>
        /// 剩余卡帧停顿帧数 (HitStop)
        /// </summary>
        public int FreezeTicks { get; private set; } = 0;
        public bool IsFreezing => FreezeTicks > 0;

        /// <summary>
        /// 朝向：是否朝左 (影响切片 SpriteRenderer.flipX 与帧偏移)
        /// </summary>
        public bool IsFacingLeft { get; set; } = false;

        private void Awake()
        {
            if (mainRenderer == null) mainRenderer = GetComponentInChildren<SpriteRenderer>();
            if (displayRoot == null) displayRoot = transform;
        }

        #region 动画资源注册与管理

        /// <summary>
        /// 注册单个动作切片资源
        /// </summary>
        public void RegisterAnim(string animKey, AnimationFrameData animData)
        {
            if (string.IsNullOrEmpty(animKey) || animData == null) return;
            _animDict[animKey] = animData;
        }

        /// <summary>
        /// 批量注册动作切片资源
        /// </summary>
        public void RegisterAnims(Dictionary<string, AnimationFrameData> anims)
        {
            if (anims == null) return;
            foreach (var kvp in anims)
            {
                RegisterAnim(kvp.Key, kvp.Value);
            }
        }

        public bool HasAnim(string animKey) => _animDict.ContainsKey(animKey);

        #endregion

        #region 动作播放与切换控制 (整数帧控制)

        /// <summary>
        /// 播放指定动作 (支持指定起始整数帧)
        /// </summary>
        /// <param name="animKey">动作名称</param>
        /// <param name="startFromFrame">从第几帧开始播放 (用于跳过前摇)</param>
        public void Play(string animKey, int startFromFrame = 0)
        {
            if (!_animDict.TryGetValue(animKey, out var animData))
            {
                Debug.LogWarning($"[FrameAnimator] 找不到动画资源: {animKey}");
                return;
            }

            CurrentAnimKey = animKey;
            CurrentAnimData = animData;
            TotalFrames = CalculateTotalFrames(animData);

            // 定位到起始帧
            startFromFrame = Mathf.Clamp(startFromFrame, 0, Mathf.Max(0, TotalFrames - 1));
            ElapsedFrames = startFromFrame;

            LocateStepByElapsedFrames(startFromFrame);
            ApplyCurrentVisualFrame();
        }

        /// <summary>
        /// 切换动作接口 (兼容 ACT 动作框架)
        /// 2D 序列帧下，无论过渡帧是否为 0，均直接硬切至目标动作指定帧
        /// </summary>
        /// <param name="animKey">动作名称</param>
        /// <param name="transitionFrames">过渡融合帧 (2D切片硬切为0)</param>
        /// <param name="startFromFrame">跳过前摇的起始整数帧</param>
        public void CrossFade(string animKey, int transitionFrames = 0, int startFromFrame = 0)
        {
            Play(animKey, startFromFrame);
        }

        /// <summary>
        /// 核心驱动：由外部逻辑帧时钟 (CharacterEntity.Tick 或 Room.Tick) 每帧显式调用
        /// </summary>
        public void Tick()
        {
            if (CurrentAnimData == null || TotalFrames <= 0) return;

            //// 1. 卡帧停顿 (HitStop) 期间，时钟完全冻结
            //if (FreezeTicks > 0)
            //{
            //    FreezeTicks--;
            //    return;
            //}

            // 2. 如果已经播完最后一帧，不再递增
            if (IsFinished)
            {
                return;
            }

            // 3. 推进总整数帧数
            ElapsedFrames++;

            // 4. 推进当前 Step 的流逝帧数并步进切片
            if (CurrentAnimData.Steps != null && CurrentAnimData.Steps.Count > 0)
            {
                CurrentStepElapsedTicks++;
                var currentStep = CurrentAnimData.Steps[CurrentStepIndex];
                int stepDuration = Mathf.Max(1, currentStep.Duration);

                if (CurrentStepElapsedTicks >= stepDuration)
                {
                    CurrentStepElapsedTicks = 0;
                    if (CurrentStepIndex < CurrentAnimData.Steps.Count - 1)
                    {
                        CurrentStepIndex++;
                    }
                    ApplyCurrentVisualFrame();
                }
            }
        }

        private void LocateStepByElapsedFrames(int targetFrame)
        {
            CurrentStepIndex = 0;
            CurrentStepElapsedTicks = 0;

            if (CurrentAnimData == null || CurrentAnimData.Steps == null) return;

            int accumulated = 0;
            for (int i = 0; i < CurrentAnimData.Steps.Count; i++)
            {
                int duration = Mathf.Max(1, CurrentAnimData.Steps[i].Duration);
                if (targetFrame < accumulated + duration)
                {
                    CurrentStepIndex = i;
                    CurrentStepElapsedTicks = targetFrame - accumulated;
                    return;
                }
                accumulated += duration;
            }

            CurrentStepIndex = CurrentAnimData.Steps.Count - 1;
            CurrentStepElapsedTicks = 0;
        }

        private int CalculateTotalFrames(AnimationFrameData animData)
        {
            if (animData == null || animData.Steps == null) return 0;
            int total = 0;
            for (int i = 0; i < animData.Steps.Count; i++)
            {
                total += Mathf.Max(1, animData.Steps[i].Duration);
            }
            return total;
        }

        #endregion

        #region 表现层切帧与图层应用

        private void ApplyCurrentVisualFrame()
        {
            if (CurrentAnimData == null || mainRenderer == null) return;

            var frameData = CurrentAnimData.GetCurrentFrameData(CurrentStepIndex);
            if (frameData == null || frameData.Sprite == null) return;

            float p2u = 0.01f;

            // 1. 设置主切片 Sprite 与翻转
            mainRenderer.sprite = frameData.Sprite;
            mainRenderer.flipX = IsFacingLeft;

            // 2. 应用对齐脚底中心偏移 (根据朝向做水平镜像)
            if (displayRoot != null)
            {
                float offX = (IsFacingLeft ? -frameData.Offset.x : frameData.Offset.x) * p2u;
                float offY = frameData.Offset.y * p2u;
                displayRoot.localPosition = new Vector3(offX, offY, 0f);
            }
        }

        #endregion

        #region 判定盒与卡帧 API (整数帧)

        /// <summary>
        /// 获取当前切片步激活的受击盒列表 (HurtBoxes)
        /// </summary>
        public List<LogicBox> GetCurrentHurtBoxes()
        {
            if (CurrentAnimData == null) return null;
            return CurrentAnimData.GetHurtBoxes(CurrentStepIndex);
        }

        /// <summary>
        /// 获取当前切片步激活的攻击盒列表 (HitBoxes)
        /// </summary>
        public List<LogicBox> GetCurrentHitBoxes()
        {
            if (CurrentAnimData == null) return null;
            return CurrentAnimData.GetHitBoxes(CurrentStepIndex);
        }

        /// <summary>
        /// 判定当前流逝的帧数是否落在指定区间内 [startFrame, endFrame]
        /// </summary>
        public bool IsInFrameRange(int startFrame, int endFrame)
        {
            return ElapsedFrames >= startFrame && ElapsedFrames <= endFrame;
        }

        /// <summary>
        /// 施加攻击卡帧或受击停顿 (以整数逻辑帧为单位)
        /// </summary>
        public void SetFreezing(int freezeTicks)
        {
            FreezeTicks = Mathf.Max(FreezeTicks, freezeTicks);
        }

        /// <summary>
        /// 重置动画状态
        /// </summary>
        public void Reset()
        {
            CurrentAnimKey = string.Empty;
            CurrentAnimData = null;
            ElapsedFrames = 0;
            TotalFrames = 0;
            CurrentStepIndex = 0;
            CurrentStepElapsedTicks = 0;
            FreezeTicks = 0;
        }

        #endregion
    }
}
