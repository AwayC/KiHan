using System;
using System.IO;
using System.Text;

namespace KiHan.Network
{
    /// <summary>
    /// 轻量级 Protobuf 写入工具
    /// </summary>
    public class ProtoWriter
    {
        private MemoryStream _stream;

        public ProtoWriter()
        {
            _stream = new MemoryStream();
        }

        public void WriteVarint(int fieldNumber, int value)
        {
            WriteTag(fieldNumber, 0); // WireType = 0 (Varint)
            WriteRawVarint((ulong)value);
        }

        public void WriteString(int fieldNumber, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            WriteTag(fieldNumber, 2); // WireType = 2 (Length-delimited)
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteRawVarint((ulong)bytes.Length);
            _stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteMessage(int fieldNumber, byte[] messageData)
        {
            if (messageData == null || messageData.Length == 0) return;
            WriteTag(fieldNumber, 2); // WireType = 2 (Length-delimited)
            WriteRawVarint((ulong)messageData.Length);
            _stream.Write(messageData, 0, messageData.Length);
        }

        private void WriteTag(int fieldNumber, int wireType)
        {
            int tag = (fieldNumber << 3) | wireType;
            WriteRawVarint((ulong)tag);
        }

        private void WriteRawVarint(ulong value)
        {
            while (value > 127)
            {
                _stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            _stream.WriteByte((byte)value);
        }

        public byte[] ToArray()
        {
            return _stream.ToArray();
        }
    }

    /// <summary>
    /// 轻量级 Protobuf 读取工具
    /// </summary>
    public class ProtoReader
    {
        private MemoryStream _stream;

        public ProtoReader(byte[] data)
        {
            _stream = new MemoryStream(data);
        }

        public bool ReadTag(out int fieldNumber, out int wireType)
        {
            if (_stream.Position >= _stream.Length)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            ulong tag = ReadRawVarint();
            fieldNumber = (int)(tag >> 3);
            wireType = (int)(tag & 0x07);
            return true;
        }

        public int ReadInt32()
        {
            return (int)ReadRawVarint();
        }

        public string ReadString()
        {
            int length = (int)ReadRawVarint();
            byte[] bytes = new byte[length];
            _stream.Read(bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        public byte[] ReadMessage()
        {
            int length = (int)ReadRawVarint();
            byte[] bytes = new byte[length];
            _stream.Read(bytes, 0, length);
            return bytes;
        }

        private ulong ReadRawVarint()
        {
            ulong value = 0;
            int shift = 0;
            while (true)
            {
                int b = _stream.ReadByte();
                if (b == -1) break; // EOF
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return value;
        }
    }
}
