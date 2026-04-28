using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 模拟按键配表，直接使用协议中的 ButtonMask
    /// </summary>
    public static class InputConfig
    {
        public static readonly Dictionary<ButtonMask, KeyCode> DefaultKeyMap = new Dictionary<ButtonMask, KeyCode>
        {
            { ButtonMask.Attack, KeyCode.J },
            { ButtonMask.Skill1, KeyCode.U },
            { ButtonMask.Skill2, KeyCode.I },
            { ButtonMask.Ultimate, KeyCode.O },
            { ButtonMask.Substitution, KeyCode.Space }, // 替身
            { ButtonMask.Secret, KeyCode.K },       // 秘卷
            { ButtonMask.Summon, KeyCode.L },       // 通灵
        };
    }
}
