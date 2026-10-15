using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 指令按键：按钮 (可用于 press / hold / release) 与摇杆状态 (只能用于 hold)
    /// </summary>
    [Serializable]
    public enum CommandKey
    {
        None = 0,

        // 按钮：值 - 1 即 ButtonMask 的位序号
        Attack = 1,       // 普攻
        Skill1 = 2,       // 技能1
        Skill2 = 3,       // 技能2
        Ultimate = 4,     // 奥义
        Substitution = 5, // 替身
        Secret = 6,       // 秘卷
        Summon = 7,       // 通灵

        // 摇杆状态 (只看当前帧)
        Move = 20,     // 任意方向
        Up = 21,       // 纵深向上 (含左上 / 右上)
        Down = 22,     // 纵深向下 (含左下 / 右下)
        Forward = 23,  // 朝向前方 (含斜向，按角色当前朝向换算)
        Backward = 24, // 朝向后方 (含斜向，按角色当前朝向换算)
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
    /// 角色指令解析器：100% 确定性，由房间逻辑帧 (BattleWorld) 显式灌入输入
    /// - 摇杆：当前方向分量 DirX / DirY，供移动、转向与摇杆状态键
    /// - 按钮：当前是否按住、最近一次按下 (动作切换时用掉)、最近一次松开 (同一个键再次按下时作废)
    /// 每个按钮只保留最近一次按下 / 松开，定长数组存储，0 GC
    /// </summary>
    public class InputToCommand : MonoBehaviour
    {
        private const int ButtonCount = 7;

        /// <summary>
        /// 指令未配置预输入时长时的默认逻辑帧数 (0.2 秒)
        /// </summary>
        public const int DefaultValidTicks = GameConfig.LOGIC_FPS / 5;

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

        /// <summary>
        /// 当前所属的房间逻辑帧编号 (由外部驱动灌入)
        /// </summary>
        public uint CurrentFrameId { get; private set; }

        private ButtonMask _lastButtons = ButtonMask.None;
        private ButtonMask _holdingButtons = ButtonMask.None;

        private readonly uint[] _pressFrame = new uint[ButtonCount];
        private readonly bool[] _pressPending = new bool[ButtonCount];
        private readonly uint[] _releaseFrame = new uint[ButtonCount];
        private readonly bool[] _releasePending = new bool[ButtonCount];

        /// <summary>
        /// 灌入当前逻辑帧的输入数据 (由 BattleWorld.Tick 显式调用)
        /// </summary>
        public void FeedInput(InputFrame input, uint currentFrameId)
        {
            CurrentFrameId = currentFrameId;
            if (input == null) return;

            UpdateDirection(input.JoyStickDir);
            UpdateButtons(input.Buttons, currentFrameId);
        }

        private void UpdateDirection(byte dir)
        {
            if (JoystickDir.TryGetAxes(dir, out int x, out int y))
            {
                JoyStickDir = dir;
                DirX = x;
                DirY = y;
            }
            else
            {
                JoyStickDir = JoystickDir.None;
                DirX = 0;
                DirY = 0;
            }
        }

        private void UpdateButtons(ButtonMask currentButtons, uint frameId)
        {
            ButtonMask pressed = currentButtons & ~_lastButtons;
            ButtonMask released = _lastButtons & ~currentButtons;
            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonMask bit = (ButtonMask)(1 << i);
                if ((pressed & bit) != 0)
                {
                    _pressFrame[i] = frameId;
                    _pressPending[i] = true;
                    // 再次按下：之前的松开作废 (连按时只有最后一次按下之后的松开才有效)
                    _releasePending[i] = false;
                }
                if ((released & bit) != 0)
                {
                    _releaseFrame[i] = frameId;
                    _releasePending[i] = true;
                }
            }

            _lastButtons = currentButtons;
            _holdingButtons = currentButtons;
        }

        /// <summary>
        /// 判定指令：press / hold / release 中的条件全部满足；没有任何条件的指令不满足
        /// </summary>
        /// <param name="facingLeft">角色当前朝向，用于换算 Forward / Backward</param>
        public bool ActionOccur(ActionCommand command, bool facingLeft = false)
        {
            bool any = false;
            int validTicks = command.validTicks > 0 ? command.validTicks : DefaultValidTicks;

            if (command.press != null)
            {
                foreach (CommandKey key in command.press)
                {
                    if (!WithinWindow(_pressPending, _pressFrame, key, validTicks)) return false;
                    any = true;
                }
            }
            if (command.release != null)
            {
                foreach (CommandKey key in command.release)
                {
                    if (!WithinWindow(_releasePending, _releaseFrame, key, validTicks)) return false;
                    any = true;
                }
            }
            if (command.hold != null)
            {
                foreach (CommandKey key in command.hold)
                {
                    if (!IsHeld(key, facingLeft)) return false;
                    any = true;
                }
            }
            return any;
        }

        private bool WithinWindow(bool[] pending, uint[] frames, CommandKey key, int validTicks)
        {
            int i = ButtonIndex(key);
            return i >= 0 && pending[i] && CurrentFrameId - frames[i] <= (uint)validTicks;
        }

        /// <summary>
        /// 当前是否按住：按钮为按住，摇杆状态键按当前方向分量判断 (Forward / Backward 按朝向换算)
        /// </summary>
        public bool IsHeld(CommandKey key, bool facingLeft = false)
        {
            switch (key)
            {
                case CommandKey.Move: return HasDirectionInput;
                case CommandKey.Up: return DirY > 0;
                case CommandKey.Down: return DirY < 0;
                case CommandKey.Forward: return DirX == (facingLeft ? -1 : 1);
                case CommandKey.Backward: return DirX == (facingLeft ? 1 : -1);
            }
            int i = ButtonIndex(key);
            return i >= 0 && (_holdingButtons & (ButtonMask)(1 << i)) != 0;
        }

        /// <summary>
        /// 按钮键在 ButtonMask 中的位序号；非按钮返回 -1
        /// </summary>
        public static int ButtonIndex(CommandKey key)
        {
            int i = (int)key - (int)CommandKey.Attack;
            return i >= 0 && i < ButtonCount ? i : -1;
        }

        /// <summary>
        /// 是否为摇杆状态键 (只能用于 hold)
        /// </summary>
        public static bool IsStickKey(CommandKey key) => key >= CommandKey.Move && key <= CommandKey.Backward;

        /// <summary>
        /// 动作切换时用掉所有按下，防止一次按下触发多个动作
        /// 保留松开：松开在新动作中仍可匹配 (如蓄力动作开始前就已松开，进入蓄力后立即释放)，直到同一个键再次按下或超出时间窗口
        /// 不重置上一帧按键状态：按住跨越动作切换时不会被当成再次按下，之后松开仍能正确记录
        /// </summary>
        public void ConsumePresses()
        {
            Array.Clear(_pressPending, 0, ButtonCount);
        }

        public void Reset()
        {
            Array.Clear(_pressPending, 0, ButtonCount);
            Array.Clear(_releasePending, 0, ButtonCount);
            JoyStickDir = JoystickDir.None;
            DirX = 0;
            DirY = 0;
            _lastButtons = ButtonMask.None;
            _holdingButtons = ButtonMask.None;
        }
    }
}
