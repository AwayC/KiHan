using System;

namespace KiHan.Logic
{
    /// <summary>
    /// 动作触发指令：press / hold / release 三组条件全部满足才成立 (同一动作的多条指令满足任意一条即可)
    /// - press：按钮在最近 validTicks 帧内按下过，且这次按下还没被动作切换用掉；多个键即同时按 (不分先后)
    /// - hold：按钮当前按住，或摇杆当前处于该状态
    /// - release：按钮在最近 validTicks 帧内松开过 (同一个键再次按下后作废)
    /// </summary>
    [Serializable]
    public struct ActionCommand
    {
        public CommandKey[] press;
        public CommandKey[] hold;
        public CommandKey[] release;

        /// <summary>
        /// press / release 的预输入逻辑帧数；<= 0 使用 InputToCommand.DefaultValidTicks
        /// </summary>
        public int validTicks;
    }
}
