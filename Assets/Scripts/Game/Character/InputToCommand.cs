using System;
using System.Collections.Generic;
using KiHan.Logic;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 统一格斗指令键枚举 (8 向方位与功能按键)
    /// </summary>
    [Serializable]
    public enum CommandKey
    {
        None = 0,

        // 相对方向：只出现在指令配置中，匹配时按角色当前朝向换算为绝对方向 (输入历史只记录绝对方向)
        Forward = 1,      // 前
        Backward = 2,     // 后
        ForwardUp = 3,    // 前上
        ForwardDown = 4,  // 前下
        BackwardUp = 5,   // 后上
        BackwardDown = 6, // 后下

        // 状态键：摇杆当前有任意方向输入 (只看当前状态，不记入输入历史)
        Move = 7,

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

        // 绝对 8 方向 (输入历史与 CurrentDirection 记录的方向)：Right + JoystickDir 编码
        Right = 30,
        UpRight = 31,
        Up = 32,
        UpLeft = 33,
        Left = 34,
        DownLeft = 35,
        Down = 36,
        DownRight = 37,
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
    /// 摇杆 8 方向编码 (网络输入字节 InputFrame.JoyStickDir)：从正右开始逆时针每 45° 一档
    /// 0 右, 1 右上, 2 上, 3 左上, 4 左, 5 左下, 6 下, 7 右下；None (255) 为无输入，其余值同样视为无输入
    /// 横向与纵深各自只有 -1 / 0 / 1 三档，移动时两轴分别乘各自速度 (斜向不做归一化)
    /// </summary>
    public static class JoystickDir
    {
        public const byte None = 255;

        private static readonly int[] AxisX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] AxisY = { 0, 1, 1, 1, 0, -1, -1, -1 };

        // 下标 (x + 1) * 3 + (y + 1) -> 编码
        private static readonly byte[] FromAxesTable = { 5, 4, 3, 6, None, 2, 7, 0, 1 };

        /// <summary>
        /// 横向 / 纵深分量 (只取符号) -> 方向编码
        /// </summary>
        public static byte FromAxes(int x, int y)
        {
            int sx = x > 0 ? 1 : x < 0 ? -1 : 0;
            int sy = y > 0 ? 1 : y < 0 ? -1 : 0;
            return FromAxesTable[(sx + 1) * 3 + (sy + 1)];
        }

        /// <summary>
        /// 方向编码 -> 横向 / 纵深分量；无输入或非法编码返回 false (分量为 0)
        /// </summary>
        public static bool TryGetAxes(byte dir, out int x, out int y)
        {
            if (dir >= AxisX.Length)
            {
                x = 0;
                y = 0;
                return false;
            }
            x = AxisX[dir];
            y = AxisY[dir];
            return true;
        }
    }

    /// <summary>
    /// 角色专属格斗指令解析器 (InputToCommand)
    /// 100% 确定性：无 Unity Update()，完全由外部房间逻辑帧 (RoomFrame.FrameId) 显式驱动
    /// - 摇杆只有 8 个方向 (JoystickDir)：当前方向分量 DirX / DirY 供移动与转向
    /// - 方向切换与按键按下 / 松开作为离散事件记入帧历史，供搓招判定
    /// </summary>
    public class InputToCommand : MonoBehaviour
    {
        [Header("历史记录最多保留的逻辑帧数")]
        [SerializeField] private uint maxKeepTicks = GameConfig.LOGIC_FPS * 3; // 3 秒

        /// <summary>
        /// 指令未配置有效窗口时的默认逻辑帧数 (0.4 秒)
        /// </summary>
        private const int DefaultValidTicks = GameConfig.LOGIC_FPS * 2 / 5;

        // ------------------ 当前摇杆状态 (8 方向) ------------------
        /// <summary>
        /// 网络摇杆方向字节 (JoystickDir 编码 0~7，JoystickDir.None 为无输入)
        /// </summary>
        public byte JoyStickDir { get; private set; } = JoystickDir.None;

        /// <summary>
        /// 摇杆横向分量：-1 左，0 无，1 右
        /// </summary>
        public int DirX { get; private set; }

        /// <summary>
        /// 摇杆纵深分量：-1 下 (纵深减小)，0 无，1 上 (纵深增大)
        /// </summary>
        public int DirY { get; private set; }

        /// <summary>
        /// 摇杆当前是否有有效方向输入
        /// </summary>
        public bool HasDirectionInput => JoyStickDir != JoystickDir.None;

        /// <summary>
        /// 摇杆水平方向 (供转向)：-1 偏左，1 偏右，0 无输入或正上/正下
        /// </summary>
        public int HorizontalSign => DirX;

        // ------------------ 离散时序历史 ------------------
        /// <summary>
        /// 当前所属的房间逻辑帧编号 (由外部驱动灌入)
        /// </summary>
        public uint CurrentFrameId { get; private set; } = 0;

        /// <summary>
        /// 当前摇杆所在的绝对方向 (None 或 8 个绝对方向之一)，与角色朝向无关
        /// </summary>
        public CommandKey CurrentDirection { get; private set; } = CommandKey.None;

        private readonly List<KeyInputRecord> _inputHistory = new List<KeyInputRecord>(48);
        private ButtonMask _lastButtons = ButtonMask.None;
        private CommandKey _lastDirection = CommandKey.None;
        private ButtonMask _holdingButtons = ButtonMask.None;

        // 按键按住的持续逻辑帧计数
        private readonly Dictionary<CommandKey, int> _keyHoldTicks = new();

        /// <summary>
        /// 外部逻辑帧唯一驱动入口 (由 BattleWorld.Tick 显式调用)
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

            // 1. 解析 8 方向
            UpdateDirection(input.JoyStickDir);

            // 2. 方向切换边缘检测 (将新方向作为离散事件记录到历史队列)
            UpdateDirectionEdgeDetection(currentFrameId);

            // 3. 按键按下 / 松开边缘检测
            UpdateButtonPressEvents(input.Buttons, currentFrameId);

            // 4. 统计按键按住持续帧数 (Hold Ticks)
            UpdateHoldDurations(input.Buttons);

            // 5. 清理超出最大寿命的过期历史按键
            PruneHistory(currentFrameId);
        }

        private void UpdateDirection(byte dir)
        {
            if (JoystickDir.TryGetAxes(dir, out int x, out int y))
            {
                JoyStickDir = dir;
                DirX = x;
                DirY = y;
                CurrentDirection = CommandKey.Right + dir;
            }
            else
            {
                JoyStickDir = JoystickDir.None;
                DirX = 0;
                DirY = 0;
                CurrentDirection = CommandKey.None;
            }
        }

        /// <summary>
        /// 摇杆方向边缘检测：仅在方向由无变有、或切换到另一个方向的瞬间，才推入历史队列
        /// 从而支持双击前冲刺 [Forward, Forward] 与 [Down, ForwardDown, Forward] 等搓招
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
        /// 指令键 -> 输入历史中的绝对键：相对方向 (前/后/前上/前下/后上/后下) 按朝向换算，其余不变
        /// </summary>
        public static CommandKey Resolve(CommandKey key, bool facingLeft)
        {
            switch (key)
            {
                case CommandKey.Forward: return facingLeft ? CommandKey.Left : CommandKey.Right;
                case CommandKey.Backward: return facingLeft ? CommandKey.Right : CommandKey.Left;
                case CommandKey.ForwardUp: return facingLeft ? CommandKey.UpLeft : CommandKey.UpRight;
                case CommandKey.ForwardDown: return facingLeft ? CommandKey.DownLeft : CommandKey.DownRight;
                case CommandKey.BackwardUp: return facingLeft ? CommandKey.UpRight : CommandKey.UpLeft;
                case CommandKey.BackwardDown: return facingLeft ? CommandKey.DownRight : CommandKey.DownLeft;
                default: return key;
            }
        }

        /// <summary>
        /// 是否为绝对 8 方向键
        /// </summary>
        public static bool IsDirectionKey(CommandKey key) => key >= CommandKey.Right && key <= CommandKey.DownRight;

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
        /// 查询按键当前是否处于按住状态
        /// </summary>
        public bool IsKeyHolding(CommandKey key)
        {
            return GetKeyHoldTicks(key) > 0;
        }

        /// <summary>
        /// 当前是否按住：Move 为摇杆有任意方向，方向键为当前摇杆方向 (相对方向按朝向换算，8 方向精确匹配)，其余为功能键按住
        /// </summary>
        public bool IsHeld(CommandKey key, bool facingLeft = false)
        {
            key = Resolve(key, facingLeft);
            if (key == CommandKey.Move) return HasDirectionInput;
            if (IsDirectionKey(key)) return CurrentDirection == key;
            return IsKeyHolding(key);
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
            sb.AppendLine($"摇杆: {(HasDirectionInput ? CommandKeyToDisplayName(CurrentDirection) : "【居中】")} | 分量: ({DirX}, {DirY})");

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
                CommandKey.ForwardUp => "【前上】",
                CommandKey.ForwardDown => "【前下】",
                CommandKey.BackwardUp => "【后上】",
                CommandKey.BackwardDown => "【后下】",
                CommandKey.Right => "【→ 右】",
                CommandKey.UpRight => "【↗ 右上】",
                CommandKey.Up => "【↑ 上】",
                CommandKey.UpLeft => "【↖ 左上】",
                CommandKey.Left => "【← 左】",
                CommandKey.DownLeft => "【↙ 左下】",
                CommandKey.Down => "【↓ 下】",
                CommandKey.DownRight => "【↘ 右下】",
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
            JoyStickDir = JoystickDir.None;
            DirX = 0;
            DirY = 0;
            CurrentDirection = CommandKey.None;
            _lastButtons = ButtonMask.None;
            _lastDirection = CommandKey.None;
            _holdingButtons = ButtonMask.None;
        }
    }
}
