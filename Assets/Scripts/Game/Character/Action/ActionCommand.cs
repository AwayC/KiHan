using System;
using TrueSync;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 招式输入指令定义 (基于定点数有效时间窗口)
    /// </summary>
    [Serializable]
    public struct ActionCommand
    {
        [Tooltip("按键顺序序列 (使用 CommandKey: 如 [Attack], [Up, Attack], [Skill1, Attack])")]
        public CommandKey[] keySequence;

        [Tooltip("整套搓招序列在多少秒内有效 (定点数秒，如 0.15s)")]
        public FP validInSec;

        public ActionCommand(CommandKey key, FP validInSec)
        {
            this.keySequence = new[] { key };
            this.validInSec = validInSec;
        }

        public ActionCommand(CommandKey[] sequence, FP validInSec)
        {
            this.keySequence = sequence;
            this.validInSec = validInSec;
        }
        public ActionCommand(CommandKey key, string validInSec = "0.15")
        {
            this.keySequence = new[] { key };
            this.validInSec = FP.Parse(validInSec);
        }

        public ActionCommand(CommandKey[] sequence, string validInSec = "0.2")
        {
            this.keySequence = sequence;
            this.validInSec = FP.Parse(validInSec);
        }

    }
}
