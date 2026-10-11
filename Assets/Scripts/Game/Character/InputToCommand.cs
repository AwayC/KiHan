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

        // 前/后：相对朝向，只出现在指令配置中，匹配时按角色当前朝向换算为左/右 (输入历史只记录绝对方向)
        Forward = 1,  // 前
        Backward = 2, // 后
        Up = 3,       // 上
        Down = 4,     // 下

        // 状态键：摇杆当前有任意方向输入 (只看当前状态，不记入输入历史)
        Move = 5,

        // 绝对水平方向 (输入历史与 CurrentDirection 记录的方向)
        Left = 6,
        Right = 7,

        // 战斗功能键 (与 ButtonMask 对应)
        Attack = 10,       // 普攻
        Skill1 = 11,       // 技能1
        Skill2 = 12,       // 技能2
        Ultimate = 13,     // 奥义
        Substitution = 14, // 替身
        Secret = 15,       // 秘卷
        Summon = 16,       // 通灵

        // 功能键松开 (松开瞬间记入输入历史，与按下事件同样参与顺序匹配)
        AttackRelease = 20,
        Skill1Release = 21,
        Skill2Release = 22,
        UltimateRelease = 23,
        SubstitutionRelease = 24,
        SecretRelease = 25,
        SummonRelease = 26,
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
        [Header("历史记录最多保留的逻辑帧数")]
        [SerializeField] private uint maxKeepTicks = GameConfig.LOGIC_FPS * 3; // 3 秒

        /// <summary>
        /// 指令未配置有效窗口时的默认逻辑帧数 (0.4 秒)
        /// </summary>
        private const int DefaultValidTicks = GameConfig.LOGIC_FPS * 2 / 5;

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
        /// 摇杆水平方向 (按整数角度判定，确定性)：-1 偏左，1 偏右，0 无输入或正上/正下
        /// </summary>
        public int HorizontalSign
        {
            get
            {
                if (JoyStickAngle == 255) return 0;
                int deg = JoyStickAngle * 2;
                if (deg == 90 || deg == 270) return 0;
                return deg > 90 && deg < 270 ? -1 : 1;
            }
        }

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
        /// 当前摇杆所在的绝对方位 (None, Left, Right, Up, Down)，与角色朝向无关
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
                return CommandKey.Right;
            }
            else
            {
                return CommandKey.Left;
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

            // 松开边缘：上一帧按住、本帧未按住
            ButtonMask released = _lastButtons & ~currentButtons;
            if ((released & ButtonMask.Attack) != 0) PushRecord(CommandKey.AttackRelease, frameId);
            if ((released & ButtonMask.Skill1) != 0) PushRecord(CommandKey.Skill1Release, frameId);
            if ((released & ButtonMask.Skill2) != 0) PushRecord(CommandKey.Skill2Release, frameId);
            if ((released & ButtonMask.Ultimate) != 0) PushRecord(CommandKey.UltimateRelease, frameId);
            if ((released & ButtonMask.Substitution) != 0) PushRecord(CommandKey.SubstitutionRelease, frameId);
            if ((released & ButtonMask.Secret) != 0) PushRecord(CommandKey.SecretRelease, frameId);
            if ((released & ButtonMask.Summon) != 0) PushRecord(CommandKey.SummonRelease, frameId);

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
            // 同一个键再次按下：作废它之前的松开记录 (连按时只有最后一次按下之后的松开才有效)
            if (key >= CommandKey.Attack && key <= CommandKey.Summon)
            {
                CommandKey release = key + (CommandKey.AttackRelease - CommandKey.Attack);
                for (int i = _inputHistory.Count - 1; i >= 0; i--)
                {
                    if (_inputHistory[i].Key == release) _inputHistory.RemoveAt(i);
                }
            }

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
        /// 指令键 -> 输入历史中的绝对键：前/后按朝向换算为左/右，其余不变
        /// </summary>
        public static CommandKey Resolve(CommandKey key, bool facingLeft)
        {
            switch (key)
            {
                case CommandKey.Forward: return facingLeft ? CommandKey.Left : CommandKey.Right;
                case CommandKey.Backward: return facingLeft ? CommandKey.Right : CommandKey.Left;
                default: return key;
            }
        }

        /// <summary>
        /// 判定招式指令序列 (ActionCommand) 是否在有效时间窗口内触发
        /// </summary>
        /// <param name="facingLeft">角色当前朝向，用于把指令中的前/后换算为左/右</param>
        public bool ActionOccur(ActionCommand command, bool facingLeft = false)
        {
            if (command.keySequence == null || command.keySequence.Length == 0) return false;

            // 状态键 (Move) 只看当前状态，必须全部满足；其余事件键 (按下/松开/方向) 在输入历史中按顺序匹配
            bool hasEventKey = false;
            foreach (CommandKey key in command.keySequence)
            {
                if (IsStateKey(key))
                {
                    if (!IsHeld(key, facingLeft)) return false;
                }
                else hasEventKey = true;
            }
            if (!hasEventKey) return true;

            int seqIdx = PrevEventKeyIndex(command.keySequence, command.keySequence.Length - 1);
            int validTicks = command.validInSec > FP.Zero 
                ? (int)FP.Ceiling(command.validInSec * GameConfig.LOGIC_FPS) 
                : DefaultValidTicks;

            for (int i = _inputHistory.Count - 1; i >= 0; i--)
            {
                var record = _inputHistory[i];

                if (CurrentFrameId >= record.FrameId && (CurrentFrameId - record.FrameId) > (uint)validTicks)
                {
                    break; // 超出有效逻辑帧窗口
                }

                if (record.Key == Resolve(command.keySequence[seqIdx], facingLeft))
                {
                    seqIdx = PrevEventKeyIndex(command.keySequence, seqIdx - 1);
                    if (seqIdx < 0) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 状态键：只看当前是否满足，不记入输入历史
        /// </summary>
        public static bool IsStateKey(CommandKey key) => key == CommandKey.Move;

        private static int PrevEventKeyIndex(CommandKey[] sequence, int index)
        {
            while (index >= 0 && IsStateKey(sequence[index])) index--;
            return index;
        }

        /// <summary>
        /// 判定混合指令序列 (支持方向与按键混合，如 [Forward, Forward] 冲刺, [Skill1, Attack] 滑A)
        /// </summary>
        /// <param name="sequence">指令序列</param>
        /// <param name="validTicks">有效逻辑帧跨度</param>
        /// <param name="facingLeft">角色当前朝向，用于把前/后换算为左/右</param>
        public bool ActionOccur(CommandKey[] sequence, int validTicks = DefaultValidTicks, bool facingLeft = false)
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

                if (record.Key == Resolve(sequence[seqIdx], facingLeft))
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
        /// 摇杆当前是否推向左方 (135° ~ 225°)
        /// </summary>
        public bool IsLeft => CurrentDirection == CommandKey.Left;

        /// <summary>
        /// 摇杆当前是否推向右方 (315° ~ 45°)
        /// </summary>
        public bool IsRight => CurrentDirection == CommandKey.Right;

        /// <summary>
        /// 查询按键当前是否处于按住状态
        /// </summary>
        public bool IsKeyHolding(CommandKey key)
        {
            return GetKeyHoldTicks(key) > 0;
        }

        /// <summary>
        /// 当前是否按住：Move 为摇杆有任意方向，方向键为当前摇杆方位 (前/后按朝向换算)，其余为功能键按住
        /// </summary>
        public bool IsHeld(CommandKey key, bool facingLeft = false)
        {
            key = Resolve(key, facingLeft);
            switch (key)
            {
                case CommandKey.Move:
                    return HasDirectionInput;
                case CommandKey.Left:
                case CommandKey.Right:
                case CommandKey.Up:
                case CommandKey.Down:
                    return CurrentDirection == key;
                default:
                    return IsKeyHolding(key);
            }
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
                CommandKey.Left => "【← 左】",
                CommandKey.Right => "【→ 右】",
                CommandKey.Up => "【↑ 上】",
                CommandKey.Down => "【↓ 下】",
                _ => "【居中】"
            };

            sb.AppendLine($"摇杆: {(HasDirectionInput ? $"{AngleDegrees:F0}°" : "无输入")} | 方位: {dirStr}");

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
                CommandKey.Left => "【左】",
                CommandKey.Right => "【右】",
                CommandKey.Up => "【上】",
                CommandKey.Down => "【下】",
                CommandKey.Attack => "【普攻】",
                CommandKey.Skill1 => "【技能1】",
                CommandKey.Skill2 => "【技能2】",
                CommandKey.Ultimate => "【奥义】",
                CommandKey.Substitution => "【替身】",
                CommandKey.Secret => "【秘卷】",
                CommandKey.Summon => "【通灵】",
                CommandKey.AttackRelease => "【松普攻】",
                CommandKey.Skill1Release => "【松技能1】",
                CommandKey.Skill2Release => "【松技能2】",
                CommandKey.UltimateRelease => "【松奥义】",
                CommandKey.SubstitutionRelease => "【松替身】",
                CommandKey.SecretRelease => "【松秘卷】",
                CommandKey.SummonRelease => "【松通灵】",
                _ => "【-】"
            };
        }

        #endregion

        /// <summary>
        /// 动作切换时清除按键的按下记录，防止一次按下触发多个动作
        /// 保留方向记录与松开记录：松开记录在新动作中仍可匹配 (如蓄力动作开始前就已松开，进入蓄力后立即释放)，
        /// 直到同一个键再次按下或超出时间窗口
        /// 不重置按键的上一帧状态：按住跨越动作切换时不会被当成再次按下 (按住不连发)，之后松开仍能正确记录松开事件
        /// </summary>
        public void CleanNonDirectionInputs()
        {
            _inputHistory.RemoveAll(r => r.Key >= CommandKey.Attack && r.Key <= CommandKey.Summon);
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
