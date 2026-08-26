using System;
using System.IO;
using System.Text;

namespace SFMOnline
{
    internal static class MsgTypes
    {
        public const byte Hello = 1;
        public const byte Welcome = 2;
        public const byte Players = 3;
        public const byte State = 4;
        public const byte Event = 5;
        public const byte Chat = 6;
        public const byte Ping = 7;
        public const byte Pong = 8;
        public const byte Bye = 9;
        public const byte Error = 10;
        public const byte Follow = 11;
        public const byte Motion = 12;
        public const byte Control = 13;
    }

    internal sealed class Packet
    {
        public byte Type;
        public byte[] Payload;
    }

    internal sealed class WireWriter
    {
        private readonly MemoryStream _ms;
        private readonly BinaryWriter _bw;

        public WireWriter()
        {
            _ms = new MemoryStream(512);
            _bw = new BinaryWriter(_ms, Encoding.UTF8);
        }

        public byte[] ToArray() => _ms.ToArray();
        public void WriteByte(byte v) => _bw.Write(v);
        public void WriteBool(bool v) => _bw.Write(v);
        public void WriteShort(short v) => _bw.Write(v);
        public void WriteUShort(ushort v) => _bw.Write(v);
        public void WriteInt(int v) => _bw.Write(v);
        public void WriteFloat(float v) => _bw.Write(v);
        public void WriteLong(long v) => _bw.Write(v);
        public void WriteString(string s) => _bw.Write(s ?? "");
    }

    internal sealed class WireReader
    {
        private readonly BinaryReader _br;

        public WireReader(byte[] data)
        {
            _br = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
        }

        public byte ReadByte() => _br.ReadByte();
        public bool ReadBool() => _br.ReadBoolean();
        public short ReadShort() => _br.ReadInt16();
        public ushort ReadUShort() => _br.ReadUInt16();
        public int ReadInt() => _br.ReadInt32();
        public float ReadFloat() => _br.ReadSingle();
        public long ReadLong() => _br.ReadInt64();
        public string ReadString() => _br.ReadString();
        public long Remaining => _br.BaseStream.Length - _br.BaseStream.Position;
    }

    internal static class PacketCodec
    {
        public static byte[] Encode(byte type, byte[] payload)
        {
            int total = payload.Length + 1; // 长度包含类型字节
            var data = new byte[5 + payload.Length];
            data[0] = (byte)(total & 0xFF);
            data[1] = (byte)((total >> 8) & 0xFF);
            data[2] = (byte)((total >> 16) & 0xFF);
            data[3] = (byte)((total >> 24) & 0xFF);
            data[4] = type;
            Buffer.BlockCopy(payload, 0, data, 5, payload.Length);
            return data;
        }

        public static int ReadLength(byte[] head) =>
            head[0] | (head[1] << 8) | (head[2] << 16) | (head[3] << 24);

        public static bool ReadExact(Stream s, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buffer, read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }
    }
}
