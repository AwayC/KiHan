using System;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace KiHan.Logic
{
    public enum ErrCode : byte
    {
        Ok = 0,
        RoomFull = 1,
        RoomInvalid = 2,
        UnknownError = 100,
    }

    public enum ClientOpCode : byte
    {
        RoomEnterReq = 1, // + [4:uid][4:roomId][1:characterId][nick]
        PlayerFrameInput = 2, // + [6:playerInput]
        PlayerReadyReq = 3, // + null
        PlayerListReq = 4, // + null
    }

    public enum ServerOpCode : byte
    {
        RoomEnterResp = 1, // + [1:ErrCode][4:roomId][1:gameId]
        PlayerListResp = 2, // + [1:ErrCode][userInfo]
        GameFrameUpdate = 3, // + [GameFrame]
        GameStartNtf = 4, // + [4:roomId]
        GameOverNtf = 5, // + [4:roomId]
        PlayerReadyResp = 6, // [1:ErrCode]
        PlayerJoinNtf = 7, // + [4:uid][1:gameId][1:characterId][nick]
        PlayerLeftNtf = 8, // + [4:uid]
    }

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
