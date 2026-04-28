using UnityEngine;
using System.Collections.Generic;
using KiHan.Logic;

namespace Managers
{
    /// <summary>
    /// 统筹虚拟按键与实体键盘的输入管理器
    /// </summary>
    public class InputManager : UnitySingleton<InputManager>
    {
        // 虚拟 UI 输入状态
        private byte _uiJoyStickAngle = 255;
        private ButtonMask _uiPressedButtons = ButtonMask.None;

        // 对外暴露最终合并后的状态
        public byte GetJoystickAngle()
        {
            // 1. 优先检查虚拟摇杆
            if (_uiJoyStickAngle != 255) return _uiJoyStickAngle;

            // 2. 备选检查键盘 WASD
            float h = 0;
            float v = 0;
            if (Input.GetKey(KeyCode.W)) v += 1;
            if (Input.GetKey(KeyCode.S)) v -= 1;
            if (Input.GetKey(KeyCode.A)) h -= 1;
            if (Input.GetKey(KeyCode.D)) h += 1;

            if (h != 0 || v != 0)
            {
                float angle = Mathf.Atan2(v, h) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360;
                return (byte)(angle / 2);
            }

            return 255;
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

        public void SetVirtualJoystick(byte angle)
        {
            _uiJoyStickAngle = angle;
        }

        public void SetVirtualButton(ButtonMask action, bool isPressed)
        {
            if (isPressed) _uiPressedButtons |= action;
            else _uiPressedButtons &= ~action;
        }
    }
}
