// ButtonMask 的副本：原定义在 Assets/Scripts/Managers/InputManager.cs (依赖 UnityEngine.Input，无法直接编译进测试)
// 修改原枚举时必须同步修改这里
using System.Collections.Generic;
using KiHan.Logic;
using System;

[Flags]
public enum ButtonMask : byte
{
    None = 0,
    Attack = 1 << 0,
    Skill1 = 1 << 1,
    Skill2 = 1 << 2,
    Ultimate = 1 << 3,
    Substitution = 1 << 4, // 替身
    Secret = 1 << 5,       // 秘卷
    Summon = 1 << 6,        // 通灵
    Other = 1 << 7,         // 预留
}
