using System;
using System.Collections.Generic;
using KiHan.Logic;

namespace KiHan.Network
{
    public class RoomSnapshot
    {
        public int room_id;
        public string player_list_json;

        public static RoomSnapshot Deserialize(byte[] data)
        {
            RoomSnapshot obj = new RoomSnapshot();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.room_id = reader.ReadInt32();
                else if (fieldNumber == 2) obj.player_list_json = reader.ReadString();
            }
            return obj;
        }
    }

    public class EnterRoomReq
    {
        public int room_id;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (room_id != 0) writer.WriteVarint(1, room_id);
            return writer.ToArray();
        }
    }

    public class EnterRoomRsp
    {
        public int err_code;
        public RoomSnapshot snapshot;
        public int my_game_id;

        public static EnterRoomRsp Deserialize(byte[] data)
        {
            EnterRoomRsp obj = new EnterRoomRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
                else if (fieldNumber == 2) obj.snapshot = RoomSnapshot.Deserialize(reader.ReadMessage());
                else if (fieldNumber == 3) obj.my_game_id = reader.ReadInt32();
            }
            return obj;
        }
    }

    public class PlayerReadyReq
    {
        public int room_id;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (room_id != 0) writer.WriteVarint(1, room_id);
            return writer.ToArray();
        }
    }

    public class PlayerReadyRsp
    {
        public int err_code;

        public static PlayerReadyRsp Deserialize(byte[] data)
        {
            PlayerReadyRsp obj = new PlayerReadyRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
            }
            return obj;
        }
    }

    public class GameStartNtf
    {
        public int room_id;

        public static GameStartNtf Deserialize(byte[] data)
        {
            GameStartNtf obj = new GameStartNtf();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.room_id = reader.ReadInt32();
            }
            return obj;
        }
    }

    // 2004
    public class PlayerFrameInput
    {
        public byte[] raw_input;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (raw_input != null && raw_input.Length > 0)
            {
                writer.WriteMessage(1, raw_input);
            }
            return writer.ToArray();
        }
    }

    // RoomFrameUpdate (2005)
    public class RoomFrameUpdate
    {
        public RoomFrame frame;

        public static RoomFrameUpdate Deserialize(byte[] data)
        {
            RoomFrameUpdate obj = new RoomFrameUpdate();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1)
                {
                    obj.frame = DeserializeRoomFrame(reader.ReadMessage());
                }
            }
            return obj;
        }

        private static RoomFrame DeserializeRoomFrame(byte[] data)
        {
            RoomFrame frame = new RoomFrame();
            frame.InputFrames = new Dictionary<byte, InputFrame>();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) frame.FrameId = (uint)reader.ReadInt32();
                else if (fieldNumber == 2) { /* player_count */ reader.ReadInt32(); }
                else if (fieldNumber == 3) 
                {
                    // Map deserialization
                    byte[] entryData = reader.ReadMessage();
                    ProtoReader mapReader = new ProtoReader(entryData);
                    int key = 0;
                    byte[] val = null;
                    while (mapReader.ReadTag(out int mapField, out int mapWire))
                    {
                        if (mapField == 1) key = mapReader.ReadInt32();
                        else if (mapField == 2) val = mapReader.ReadMessage();
                    }

                    if (val != null && val.Length >= 2)
                    {
                        InputFrame input = new InputFrame();
                        // 2 bytes raw_input format: [Joystick(1)][Buttons(1)]
                        input.FrameId = frame.FrameId;
                        input.JoyStickAngle = val[0];
                        input.Buttons = (ButtonMask)val[1];
                        frame.InputFrames[(byte)key] = input;
                    }
                }
            }
            return frame;
        }
    }

    public class GameOverReq
    {
        public int room_id;
        public uint winner_uid;
        public int p1_hp;
        public int p2_hp;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (room_id != 0) writer.WriteVarint(1, room_id);
            if (winner_uid != 0) writer.WriteVarint(2, (int)winner_uid);
            if (p1_hp != 0) writer.WriteVarint(3, p1_hp);
            if (p2_hp != 0) writer.WriteVarint(4, p2_hp);
            return writer.ToArray();
        }
    }

    public class GameOverRsp
    {
        public int err_code;

        public static GameOverRsp Deserialize(byte[] data)
        {
            GameOverRsp obj = new GameOverRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
            }
            return obj;
        }
    }

    public class GameOverNtf
    {
        public int room_id;
        public uint winner_uid;

        public static GameOverNtf Deserialize(byte[] data)
        {
            GameOverNtf obj = new GameOverNtf();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.room_id = reader.ReadInt32();
                else if (fieldNumber == 2) obj.winner_uid = (uint)reader.ReadInt32();
            }
            return obj;
        }
    }
}