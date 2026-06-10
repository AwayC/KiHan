using System;

namespace KiHan.Network
{
    public class PlayerInfo
    {
        public uint uid;
        public string nickname;
        public string data_json;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (uid != 0) writer.WriteVarint(1, (int)uid);
            if (!string.IsNullOrEmpty(nickname)) writer.WriteString(2, nickname);
            if (!string.IsNullOrEmpty(data_json)) writer.WriteString(3, data_json);
            return writer.ToArray();
        }

        public static PlayerInfo Deserialize(byte[] data)
        {
            PlayerInfo obj = new PlayerInfo();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.uid = (uint)reader.ReadInt32();
                else if (fieldNumber == 2) obj.nickname = reader.ReadString();
                else if (fieldNumber == 3) obj.data_json = reader.ReadString();
            }
            return obj;
        }
    }

    public class LoginReq
    {
        public byte[] Serialize()
        {
            return new byte[0];
        }
    }

    public class LoginRsp
    {
        public int err_code;
        public PlayerInfo player;

        public static LoginRsp Deserialize(byte[] data)
        {
            LoginRsp obj = new LoginRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
                else if (fieldNumber == 2) obj.player = PlayerInfo.Deserialize(reader.ReadMessage());
            }
            return obj;
        }
    }

    public class CreateRoleReq
    {
        public string nickname;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (!string.IsNullOrEmpty(nickname)) writer.WriteString(1, nickname);
            return writer.ToArray();
        }
    }

    public class CreateRoleRsp
    {
        public int err_code;

        public static CreateRoleRsp Deserialize(byte[] data)
        {
            CreateRoleRsp obj = new CreateRoleRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
            }
            return obj;
        }
    }

    public class CreateRoleNtf
    {
        public static CreateRoleNtf Deserialize(byte[] data)
        {
            return new CreateRoleNtf();
        }
    }

    public class GetOnlineCountReq
    {
        public byte[] Serialize()
        {
            return new byte[0];
        }
    }

    public class GetOnlineCountRsp
    {
        public int err_code;
        public int online_count;

        public static GetOnlineCountRsp Deserialize(byte[] data)
        {
            GetOnlineCountRsp obj = new GetOnlineCountRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
                else if (fieldNumber == 2) obj.online_count = reader.ReadInt32();
            }
            return obj;
        }
    }

    public class GetPlayerDataReq
    {
        public byte[] Serialize()
        {
            return new byte[0];
        }
    }

    public class GetPlayerDataRsp
    {
        public int err_code;
        public PlayerInfo player;

        public static GetPlayerDataRsp Deserialize(byte[] data)
        {
            GetPlayerDataRsp obj = new GetPlayerDataRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
                else if (fieldNumber == 2) obj.player = PlayerInfo.Deserialize(reader.ReadMessage());
            }
            return obj;
        }
    }

    public class MatchGameReq
    {
        public int character_id;

        public byte[] Serialize()
        {
            ProtoWriter writer = new ProtoWriter();
            if (character_id != 0) writer.WriteVarint(1, character_id);
            return writer.ToArray();
        }
    }

    public class MatchGameRsp
    {
        public int err_code;

        public static MatchGameRsp Deserialize(byte[] data)
        {
            MatchGameRsp obj = new MatchGameRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
            }
            return obj;
        }
    }

    public class MatchStopReq
    {
        public byte[] Serialize()
        {
            return new byte[0];
        }
    }

    public class MatchStopRsp
    {
        public int err_code;
        public bool success;

        public static MatchStopRsp Deserialize(byte[] data)
        {
            MatchStopRsp obj = new MatchStopRsp();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
                else if (fieldNumber == 2) obj.success = reader.ReadInt32() == 1;
            }
            return obj;
        }
    }

    public class MatchGameNtf
    {
        public int err_code;
        public string room_id;
        public int position;
        public string room_snapshot_json;

        public static MatchGameNtf Deserialize(byte[] data)
        {
            MatchGameNtf obj = new MatchGameNtf();
            ProtoReader reader = new ProtoReader(data);
            while (reader.ReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1) obj.err_code = reader.ReadInt32();
                else if (fieldNumber == 2) obj.room_id = reader.ReadString();
                else if (fieldNumber == 3) obj.position = reader.ReadInt32();
                else if (fieldNumber == 4) obj.room_snapshot_json = reader.ReadString();
            }
            return obj;
        }
    }
}
