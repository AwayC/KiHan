using System;
using System.Collections.Generic;
using KiHan.Logic;
using KiHan.Math;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 统一格斗指令键枚举 (包含 4 向离散方位与功能按键)
    /// </summary>
    [Serializable]
    public enum CommandKey
    {
        None = 0,

        // 离散方位 (由摇杆角度计算，结合 Inversed 自动镜像左右)
        Forward = 1,  // 前
        Backward = 2, // 后
        Up = 3,       // 上
        Down = 4,     // 下

        // 战斗功能键 (与 ButtonMask 对应)
        Attack = 10,       // 普攻
        Skill1 = 11,       // 技能1
        Skill2 = 12,       // 技能2
        Ultimate = 13,     // 奥义
        Substitution = 14, // 替身
        Secret = 15,       // 秘卷
        Summon = 16        // 通灵
    }

    /// <summary>
    /// 单次离散输入历史记录 (基于逻辑帧时间戳)
    /// </summary>
    [Serializable]
    public struct KeyInputRecord
    {
        public CommandKey Key;
        public uint FrameId; // 房间逻辑帧编号

        public KeyInputRecord(CommandKey key, uint frameId)
        {
            Key = key;
            FrameId = frameId;
        }
    }

    /// <summary>
    /// 角色专属格斗指令解析器 (InputToCommand)
    /// 100% 确定性：无 Unity Update()，完全由外部房间逻辑帧 (RoomFrame.FrameId) 显式驱动
    /// 双轨制模型：
    /// 轨道 A (连续物理向)：保留 360° 真实角度与移动向量，供人物自由走位与技能转向
    /// 轨道 B (离散语义向)：摇杆扇区边缘跳变检测 (记录前/后/上/下) + 按键按下，记录离散帧历史供搓招判定
    /// </summary>
    public class InputToCommand : MonoBehaviour
    {
        [Header("角色朝向状态")]
        [Tooltip("是否朝左 (朝左时前后输入自动镜像翻转)")]
        public bool inversed = false;

        [Header("历史记录最多保留的逻辑帧数")]
        [SerializeField] private uint maxKeepTicks = 45; // 45帧 (15 FPS 下为 3秒)

        // ------------------ 轨道 A: 连续物理方向 (保留真实角度) ------------------
        /// <summary>
        /// 原始网络摇杆角度字节 (0~180 对应 0°~360°, 255 为无输入死区)
        /// </summary>
        public byte JoyStickAngle { get; private set; } = 255;

        /// <summary>
        /// 换算后的 360 度角度 (0° ~ 360°)
        /// </summary>
        public float AngleDegrees { get; private set; } = 0f;

        /// <summary>
        /// 摇杆当前是否有有效方向输入
        /// </summary>
        public bool HasDirectionInput => JoyStickAngle != 255;

        /// <summary>
        /// 360° 连续移动单位向量 (供表现层与常规移动)
        /// </summary>
        public Vector2 MoveDirection { get; private set; } = Vector2.zero;

        /// <summary>
        /// 360° 连续移动单位向量 (确定性定点数，供逻辑层物理)
        /// </summary>
        public FPVector2 MoveDirectionFP { get; private set; } = FPVector2.zero;

        // ------------------ 轨道 B: 离散时序历史与当前帧状态 ------------------
        /// <summary>
        /// 当前所属的房间逻辑帧编号 (由外部驱动灌入)
        /// </summary>
        public uint CurrentFrameId { get; private set; } = 0;

        /// <summary>
        /// 当前摇杆推向的意图方向 (None, Forward, Backward, Up, Down)
        /// </summary>
        public CommandKey CurrentDirection { get; private set; } = CommandKey.None;

        private readonly List<KeyInputRecord> _inputHistory = new List<KeyInputRecord>(48);
        private ButtonMask _lastButtons = ButtonMask.None;
        private CommandKey _lastDirection = CommandKey.None;
        private ButtonMask _holdingButtons = ButtonMask.None;

        // 按键按住的持续逻辑帧计数
        private readonly Dictionary<CommandKey, int> _keyHoldTicks = new();

        /// <summary>
        /// 外部逻辑帧唯一驱动入口 (由 CharacterEntity.Tick / LSRoom.Tick 显式调用)
        /// </summary>
        /// <param name="input">网络或单机同步到的当前帧数据</param>
        /// <param name="currentFrameId">房间当前的逻辑帧编号 (FrameId)</param>
        public void Tick(InputFrame input, uint currentFrameId)
        {
            FeedInput(input, currentFrameId);
        }

        /// <summary>
        /// 灌入当前逻辑帧的输入数据
        /// </summary>
        public void FeedInput(InputFrame input, uint currentFrameId)
        {
            CurrentFrameId = currentFrameId;
            if (input == null) return;

            // 1. 轨道 A：解析 360° 连续角度与向量
            UpdateContinuousAngle(input.JoyStickAngle);

            // 2. 轨道 B：摇杆扇区边缘跳变检测 (将推方向作为离散事件记录到历史队列)
            UpdateDirectionEdgeDetection(currentFrameId);

            // 3. 轨道 B：按键按下边缘检测 (Down)
            UpdateButtonPressEvents(input.Buttons, currentFrameId);

            // 4. 统计按键按住持续帧数 (Hold Ticks)
            UpdateHoldDurations(input.Buttons);

            // 5. 清理超出最大寿命的过期历史按键
            PruneHistory(currentFrameId);
        }

        private void UpdateContinuousAngle(byte angleByte)
        {
            JoyStickAngle = angleByte;

            if (angleByte == 255)
            {
                AngleDegrees = 0f;
                MoveDirection = Vector2.zero;
                MoveDirectionFP = FPVector2.zero;
                CurrentDirection = CommandKey.None;
                return;
            }

            // 0~180 对应 0°~360° (纯整型度数换算)
            int degInt = angleByte * 2;
            AngleDegrees = degInt;

            // 纯定点数确定性方向计算 (0 浮点误差与跨平台 FPU 尾数偏差)
            FP degFP = degInt;
            FP radFP = degFP * TSMath.Deg2Rad;
            MoveDirectionFP = new FPVector2(TSMath.Cos(radFP), TSMath.Sin(radFP));

            // 表现层视图平滑向量 (单向由定点数映射)
            MoveDirection = MoveDirectionFP.ToVector2();

            // 计算当前所在的离散扇区 (纯整型比较)
            CurrentDirection = CalculateDirectionSector(degInt);
        }

        private CommandKey CalculateDirectionSector(int deg)
        {
            // 4 扇区划分 (纯整数角度):
            // 上: 45° ~ 135°
            // 下: 225° ~ 315°
            // 右: 315° ~ 45°
            // 左: 135° ~ 225°
            if (deg >= 45 && deg < 135) return CommandKey.Up;
            if (deg >= 225 && deg < 315) return CommandKey.Down;

            bool isRight = (deg >= 315 || deg < 45);
            if (isRight)
            {
                return inversed ? CommandKey.Backward : CommandKey.Forward;
            }
            else
            {
                return inversed ? CommandKey.Forward : CommandKey.Backward;
            }
        }

        /// <summary>
        /// 摇杆方位边缘检测：仅在方向由无变有、或跨扇区切换瞬间，才推入历史队列
        /// 从而精确支持双击前冲刺 [Forward, Forward] 与 236 搓招
        /// </summary>
        private void UpdateDirectionEdgeDetection(uint frameId)
        {
            if (CurrentDirection != _lastDirection)
            {
                if (CurrentDirection != CommandKey.None)
                {
                    PushRecord(CurrentDirection, frameId);
                }
                _lastDirection = CurrentDirection;
            }
        }

        private void UpdateButtonPressEvents(ButtonMask currentButtons, uint frameId)
        {
            _holdingButtons = currentButtons;
            ButtonMask newPress = currentButtons & ~_lastButtons;

            if ((newPress & ButtonMask.Attack) != 0) PushRecord(CommandKey.Attack, frameId);
            if ((newPress & ButtonMask.Skill1) != 0) PushRecord(CommandKey.Skill1, frameId);
            if ((newPress & ButtonMask.Skill2) != 0) PushRecord(CommandKey.Skill2, frameId);
            if ((newPress & ButtonMask.Ultimate) != 0) PushRecord(CommandKey.Ultimate, frameId);
            if ((newPress & ButtonMask.Substitution) != 0) PushRecord(CommandKey.Substitution, frameId);
            if ((newPress & ButtonMask.Secret) != 0) PushRecord(CommandKey.Secret, frameId);
            if ((newPress & ButtonMask.Summon) != 0) PushRecord(CommandKey.Summon, frameId);

            _lastButtons = currentButtons;
        }

        private void UpdateHoldDurations(ButtonMask buttons)
        {
            UpdateSingleHold(CommandKey.Attack, (buttons & ButtonMask.Attack) != 0);
            UpdateSingleHold(CommandKey.Skill1, (buttons & ButtonMask.Skill1) != 0);
            UpdateSingleHold(CommandKey.Skill2, (buttons & ButtonMask.Skill2) != 0);
            UpdateSingleHold(CommandKey.Ultimate, (buttons & ButtonMask.Ultimate) != 0);
            UpdateSingleHold(CommandKey.Substitution, (buttons & ButtonMask.Substitution) != 0);
            UpdateSingleHold(CommandKey.Secret, (buttons & ButtonMask.Secret) != 0);
            UpdateSingleHold(CommandKey.Summon, (buttons & ButtonMask.Summon) != 0);
        }

        private void UpdateSingleHold(CommandKey key, bool isHolding)
        {
            if (isHolding)
            {
                _keyHoldTicks[key] = _keyHoldTicks.TryGetValue(key, out int t) ? t + 1 : 1;
            }
            else
            {
                _keyHoldTicks[key] = 0;
            }
        }

        private void PushRecord(CommandKey key, uint frameId)
        {
            if (_inputHistory.Count >= 48)
            {
                _inputHistory.RemoveAt(0);
            }
            _inputHistory.Add(new KeyInputRecord(key, frameId));
        }

        /// <summary>
        /// 外部/AI 虚拟注入按键 (供影分身 AI 或测试使用)
        /// </summary>
        public void PushInput(CommandKey key)
        {
            PushRecord(key, CurrentFrameId);
        }

        /// <summary>
        /// 外部/AI 虚拟注入按键 (ButtonMask 重载)
        /// </summary>
        public void PushInput(ButtonMask button)
        {
            CommandKey cmd = ButtonMaskToCommandKey(button);
            if (cmd != CommandKey.None)
            {
                PushRecord(cmd, CurrentFrameId);
            }
        }

        private static CommandKey ButtonMaskToCommandKey(ButtonMask button)
        {
            if ((button & ButtonMask.Attack) != 0) return CommandKey.Attack;
            if ((button & ButtonMask.Skill1) != 0) return CommandKey.Skill1;
            if ((button & ButtonMask.Skill2) != 0) return CommandKey.Skill2;
            if ((button & ButtonMask.Ultimate) != 0) return CommandKey.Ultimate;
            if ((button & ButtonMask.Substitution) != 0) return CommandKey.Substitution;
            if ((button & ButtonMask.Secret) != 0) return CommandKey.Secret;
            if ((button & ButtonMask.Summon) != 0) return CommandKey.Summon;
            return CommandKey.None;
        }

        #region 指令判定 API (时序与搓招匹配)
        /// <summary>
        /// 判定招式指令序列 (ActionCommand) 是否在有效时间窗口内触发
        /// </summary>
        public bool ActionOccur(ActionCommand command)
        {
            if (command.keySequence == null || command.keySequence.Length == 0) return false;

            int seqIdx = command.keySequence.Length - 1;
            int validTicks = command.validInSec > FP.Zero 
                ? (int)FP.Ceiling(command.validInSec * GameConfig.LOGIC_FPS) 
                : 6; // 默认 6 逻辑帧 (~0.4秒)

            for (int i = _inputHistory.Count - 1; i >= 0; i--)
            {
                var record = _inputHistory[i];

                if (CurrentFrameId >= record.FrameId && (CurrentFrameId - record.FrameId) > (uint)validTicks)
                {
                    break; // 超出有效逻辑帧窗口
                }

                if (record.Key == command.keySequence[seqIdx])
                {
                    seqIdx--;
                    if (seqIdx < 0) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判定混合指令序列 (支持方向与按键混合，如 [Forward, Forward] 冲刺, [Skill1, Attack] 滑A)
        /// </summary>
        /// <param name="sequence">指令序列</param>
        /// <param name="validTicks">有效逻辑帧跨度</param>
        public bool ActionOccur(CommandKey[] sequence, int validTicks = 6)
        {
            if (sequence == null || sequence.Length == 0) return false;

            int seqIdx = sequence.Length - 1;
            for (int i = _inputHistory.Count - 1; i >= 0; i--)
            {
                var record = _inputHistory[i];

                if (CurrentFrameId >= record.FrameId && (CurrentFrameId - record.FrameId) > (uint)validTicks)
                {
                    break;
                }

                if (record.Key == sequence[seqIdx])
                {
                    seqIdx--;
                    if (seqIdx < 0) return true;
                }
            }

            return false;
        }

        /// <summary>
        public bool ActionOccur(ButtonMask button)
        {
            return ActionOccur(ButtonMaskToCommandKey(button));
        }

        public bool ActionOccur(CommandKey key, string validInSec = "0.15")
        {
            return ActionOccur(new ActionCommand(key, validInSec));
        }

        public bool ActionOccur(CommandKey key, FP validInSec)
        {
            return ActionOccur(new ActionCommand(key, validInSec));
        }

        #endregion

        #region 瞬时状态与蓄力查询

        /// <summary>
        /// 摇杆当前是否推向上方 (45° ~ 135°)
        /// </summary>
        public bool IsUp => CurrentDirection == CommandKey.Up;

        /// <summary>
        /// 摇杆当前是否推向下方 (225° ~ 315°)
        /// </summary>
        public bool IsDown => CurrentDirection == CommandKey.Down;

        /// <summary>
        /// 摇杆当前是否推向面朝方向 (前推)
        /// </summary>
        public bool IsForward => CurrentDirection == CommandKey.Forward;

        /// <summary>
        /// 摇杆当前是否推向面朝反方向 (后拉)
        /// </summary>
        public bool IsBackward => CurrentDirection == CommandKey.Backward;

        /// <summary>
        /// 查询按键当前是否处于按住状态
        /// </summary>
        public bool IsKeyHolding(CommandKey key)
        {
            return GetKeyHoldTicks(key) > 0;
        }

        public int GetKeyHoldTicks(CommandKey key)
        {
            return _keyHoldTicks.TryGetValue(key, out int ticks) ? ticks : 0;
        }

        public bool IsButtonHolding(ButtonMask button) => IsKeyHolding(ButtonMaskToCommandKey(button));
        public int GetButtonHoldTicks(ButtonMask button) => GetKeyHoldTicks(ButtonMaskToCommandKey(button));

        #endregion

        #region UI 调试显示

        /// <summary>
        /// 获得输入状态与近期按键指令所组成的调试文字 (供战斗 UI 屏幕实时显示)
        /// </summary>
        public string InputText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // 1. 摇杆与方向状态
            string dirStr = CurrentDirection switch
            {
                CommandKey.Forward => inversed ? "【← 前】" : "【→ 前】",
                CommandKey.Backward => inversed ? "【→ 后】" : "【← 后】",
                CommandKey.Up => "【↑ 上】",
                CommandKey.Down => "【↓ 下】",
                _ => "【居中】"
            };

            sb.AppendLine($"摇杆: {(HasDirectionInput ? $"{AngleDegrees:F0}°" : "无输入")} | 方位: {dirStr} | 朝向: {(inversed ? "朝左" : "朝右")}");

            // 2. 近期输入指令序列 (展示最近输入的 8 个操作)
            sb.Append("指令流: ");
            if (_inputHistory.Count == 0)
            {
                sb.AppendLine("无");
            }
            else
            {
                int start = Mathf.Max(0, _inputHistory.Count - 8);
                for (int i = _inputHistory.Count - 1; i >= start; i--)
                {
                    var r = _inputHistory[i];
                    sb.Append($"{CommandKeyToDisplayName(r.Key)}(F:{r.FrameId}) ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string CommandKeyToDisplayName(CommandKey key)
        {
            return key switch
            {
                CommandKey.Forward => "【前】",
                CommandKey.Backward => "【后】",
                CommandKey.Up => "【上】",
                CommandKey.Down => "【下】",
                CommandKey.Attack => "【普攻】",
                CommandKey.Skill1 => "【技能1】",
                CommandKey.Skill2 => "【技能2】",
                CommandKey.Ultimate => "【奥义】",
                CommandKey.Substitution => "【替身】",
                CommandKey.Secret => "【秘卷】",
                CommandKey.Summon => "【通灵】",
                _ => "【-】"
            };
        }

        #endregion

        /// <summary>
        /// 动作切换时清空功能按键记录 (保留方向记录)，防止普攻连发粘连
        /// </summary>
        public void CleanNonDirectionInputs()
        {
            _inputHistory.RemoveAll(r => r.Key != CommandKey.Forward && r.Key != CommandKey.Backward && r.Key != CommandKey.Up && r.Key != CommandKey.Down);
            _lastButtons = ButtonMask.None;
        }

        private void PruneHistory(uint currentFrame)
        {
            while (_inputHistory.Count > 0 && currentFrame >= _inputHistory[0].FrameId && (currentFrame - _inputHistory[0].FrameId) > maxKeepTicks)
            {
                _inputHistory.RemoveAt(0);
            }
        }

        public void Reset()
        {
            _inputHistory.Clear();
            _keyHoldTicks.Clear();
            JoyStickAngle = 255;
            AngleDegrees = 0f;
            MoveDirection = Vector2.zero;
            MoveDirectionFP = FPVector2.zero;
            CurrentDirection = CommandKey.None;
            _lastButtons = ButtonMask.None;
            _lastDirection = CommandKey.None;
            _holdingButtons = ButtonMask.None;
        }
    }
}
