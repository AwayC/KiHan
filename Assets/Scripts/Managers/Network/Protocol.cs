using System;
using System.Collections.Generic;

namespace KiHan.Logic
{

    public class InputFrame // 6 bytes total
    {
        public const int RawDataLength = 2; // 2 bytes of raw input data from client
        public const int DataLength = 6;

        public uint FrameId; // 4 bytes
        public byte JoyStickDir; // 摇杆 8 方向编码 0~7 (JoystickDir)，255 为无移动
        public ButtonMask Buttons;

        public void Serialize(byte[] buffer, int offset)
        {
            byte[] frameIdBytes = BitConverter.GetBytes(FrameId);
            // Assuming little-endian but should be consistent
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(frameIdBytes);
            
            Array.Copy(frameIdBytes, 0, buffer, offset, 4);
            buffer[offset + 4] = JoyStickDir;
            buffer[offset + 5] = (byte)Buttons;
        }

        public void Deserialize(byte[] buffer, int offset)
        {
            byte[] frameIdBytes = new byte[4];
            Array.Copy(buffer, offset, frameIdBytes, 0, 4);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(frameIdBytes);
            
            FrameId = BitConverter.ToUInt32(frameIdBytes, 0);
            JoyStickDir = buffer[offset + 4];
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
