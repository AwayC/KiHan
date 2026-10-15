using UnityEngine;
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

namespace Managers
{
    /// <summary>
    /// 统筹虚拟按键与实体键盘的输入管理器
    /// </summary>
    public class InputManager : UnitySingleton<InputManager>
    {
        // 虚拟 UI 输入状态
        private byte _uiJoyStickDir = JoystickDir.None;
        private ButtonMask _uiPressedButtons = ButtonMask.None;

        /// <summary>
        /// 合并后的摇杆 8 方向编码 (JoystickDir)：虚拟摇杆优先，其次键盘 WASD
        /// </summary>
        public byte GetJoystickDir()
        {
            if (_uiJoyStickDir != JoystickDir.None) return _uiJoyStickDir;

            int h = 0;
            int v = 0;
            if (Input.GetKey(KeyCode.W)) v += 1;
            if (Input.GetKey(KeyCode.S)) v -= 1;
            if (Input.GetKey(KeyCode.A)) h -= 1;
            if (Input.GetKey(KeyCode.D)) h += 1;
            return JoystickDir.FromAxes(h, v);
        }

        public ButtonMask GetCombinedButtons()
        {
            ButtonMask result = _uiPressedButtons;

            // 轮询配表中的按键，合并物理键盘输入
            foreach (var kvp in InputConfig.DefaultKeyMap)
            {
                if (Input.GetKey(kvp.Value))
                {
                    result |= kvp.Key;
                }
            }

            return result;
        }

        // --- 供 UI 组件调用的接口 ---

        /// <summary>
        /// 虚拟摇杆方向 (JoystickDir 编码，JoystickDir.None 为松开或死区)
        /// </summary>
        public void SetVirtualJoystick(byte dir)
        {
            _uiJoyStickDir = dir;
        }

        public void SetVirtualButton(ButtonMask action, bool isPressed)
        {
            if (isPressed) _uiPressedButtons |= action;
            else _uiPressedButtons &= ~action;
        }
    }
}
