using System;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 分身大脑类型
    /// </summary>
    public enum CloneBrainType
    {
        /// <summary>
        /// 不注入输入，只按动作链 (autoNext / 落地 / 命中) 执行
        /// </summary>
        None = 0,

        /// <summary>
        /// 按脚本在指定逻辑帧注入按键与方向
        /// </summary>
        Script = 1,
    }

    /// <summary>
    /// 分身脚本的一步：从生成后第 frame 个逻辑帧起，持续 hold 帧按住 buttons 与方向 dir
    /// (按键在第一帧形成"按下"事件，结束后形成"松开"事件，与玩家输入的语义一致)
    /// </summary>
    public struct CloneScriptStep
    {
        public int frame;
        public int hold;
        public ButtonMask buttons;

        /// <summary>
        /// 摇杆方向：Forward / Backward 按分身当前朝向换算为横向分量，Up / Down 为纵深分量；None 表示不推该轴
        /// </summary>
        public CommandKey dirX;
        public CommandKey dirY;
    }

    [Serializable]
    public struct CloneScriptStepData
    {
        [Tooltip("生成后的逻辑帧序号 (0 为生成后的第一帧)")]
        public int frame;

        [Tooltip("按住的逻辑帧数，<= 0 按 1 帧")]
        public int hold;

        [Tooltip("按键名 (ButtonMask)，如 [\"Attack\"]")]
        public string[] buttons;

        [Tooltip("摇杆方向 (Forward / Backward / Up / Down，可组合斜向)，如 [\"Forward\", \"Up\"]，留空不推摇杆")]
        public string[] dir;

        public CloneScriptStep ToRuntime()
        {
            ButtonMask mask = ButtonMask.None;
            if (buttons != null)
            {
                foreach (string b in buttons) mask |= ActionData.ParseEnum(b, ButtonMask.None);
            }

            CommandKey dirX = CommandKey.None, dirY = CommandKey.None;
            if (dir != null)
            {
                foreach (string d in dir)
                {
                    CommandKey key = ActionData.ParseEnum(d, CommandKey.None);
                    if (key == CommandKey.Forward || key == CommandKey.Backward) dirX = key;
                    else if (key == CommandKey.Up || key == CommandKey.Down) dirY = key;
                    else Debug.LogError($"[CloneBrain] 无法识别的摇杆方向: \"{d}\"，可选值: Forward, Backward, Up, Down");
                }
            }

            return new CloneScriptStep
            {
                frame = frame,
                hold = hold > 0 ? hold : 1,
                buttons = mask,
                dirX = dirX,
                dirY = dirY
            };
        }
    }

    /// <summary>
    /// 分身大脑：每个逻辑帧在输入阶段为没有网络输入槽位的实体生成一帧输入 (复用同一个 InputFrame，0 GC)
    /// </summary>
    public class CloneBrain
    {
        private readonly InputFrame _frame = new InputFrame();
        private CloneBrainType _type;
        private CloneScriptStep[] _script = Array.Empty<CloneScriptStep>();
        private int _elapsed;

        public void Setup(CloneBrainType type, CloneScriptStep[] script)
        {
            _type = type;
            _script = script ?? Array.Empty<CloneScriptStep>();
            _elapsed = 0;
        }

        /// <summary>
        /// 生成本帧输入；不注入输入的大脑返回 null
        /// </summary>
        public InputFrame Think(CharacterObj self)
        {
            if (_type != CloneBrainType.Script) return null;

            ButtonMask buttons = ButtonMask.None;
            int x = 0, y = 0;
            for (int i = 0; i < _script.Length; i++)
            {
                CloneScriptStep step = _script[i];
                if (_elapsed >= step.frame && _elapsed < step.frame + step.hold)
                {
                    buttons |= step.buttons;
                    if (step.dirX != CommandKey.None) x = (step.dirX == CommandKey.Forward) != self.FacingLeft ? 1 : -1;
                    if (step.dirY != CommandKey.None) y = step.dirY == CommandKey.Up ? 1 : -1;
                }
            }
            _elapsed++;

            _frame.Buttons = buttons;
            _frame.JoyStickDir = JoystickDir.FromAxes(x, y);
            return _frame;
        }
    }
}
