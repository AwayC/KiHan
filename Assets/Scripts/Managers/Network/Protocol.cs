using System;
using System.Collections.Generic;

namespace KiHan.Logic
{
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

    public class InputFrame // 6 bytes total
    {
        public const int RawDataLength = 2; // 2 bytes of raw input data from client
        public const int DataLength = 6;

        public uint FrameId; // 4 bytes
        public byte JoyStickAngle; // 0-180 , 255 为无移动
        public ButtonMask Buttons;

        public void Serialize(byte[] buffer, int offset)
        {
            byte[] frameIdBytes = BitConverter.GetBytes(FrameId);
            // Assuming little-endian but should be consistent
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(frameIdBytes);
            
            Array.Copy(frameIdBytes, 0, buffer, offset, 4);
            buffer[offset + 4] = JoyStickAngle;
            buffer[offset + 5] = (byte)Buttons;
        }

        public void Deserialize(byte[] buffer, int offset)
        {
            byte[] frameIdBytes = new byte[4];
            Array.Copy(buffer, offset, frameIdBytes, 0, 4);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(frameIdBytes);
            
            FrameId = BitConverter.ToUInt32(frameIdBytes, 0);
            JoyStickAngle = buffer[offset + 4];
            Buttons = (ButtonMask)buffer[offset + 5];
        }
    }

    public class RoomFrame
    {
        public uint FrameId = 1; // 4 bytes
        public int PlayerCount; // 1 byte, 0-255
        public Dictionary<byte, InputFrame> InputFrames; // <gameId, InputFrame>, InputFrame 2 bytes, no frameId
    }
}
