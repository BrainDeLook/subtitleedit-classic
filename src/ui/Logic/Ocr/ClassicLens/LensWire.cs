using System;
using System.IO;
using System.Text;

namespace Nikse.SubtitleEdit.Logic.Ocr.ClassicLens
{
    // The small wire subset used by the stable Lens messages avoids a new runtime dependency.
    internal static class WireFormat
    {
        internal enum WireType { Varint = 0, Fixed64 = 1, LengthDelimited = 2, Fixed32 = 5 }
        internal static int GetTagFieldNumber(uint tag) => (int)(tag >> 3);
    }
    internal sealed class ByteString
    {
        private readonly byte[] _bytes;
        private ByteString(byte[] bytes) { _bytes = bytes; }
        internal int Length => _bytes.Length;
        internal static ByteString CopyFrom(byte[] bytes) => new ByteString(bytes);
        internal byte[] ToByteArray() => _bytes;
    }
    internal sealed class CodedOutputStream : IDisposable
    {
        private readonly Stream _stream;
        internal CodedOutputStream(Stream stream) { _stream = stream; }
        internal void WriteUInt64(ulong value)
        {
            while (value >= 128) { _stream.WriteByte((byte)(value | 128)); value >>= 7; }
            _stream.WriteByte((byte)value);
        }
        internal void WriteTag(int field, WireFormat.WireType wire) => WriteUInt64((ulong)((field << 3) | (int)wire));
        internal void WriteInt32(int value) => WriteUInt64(unchecked((ulong)(long)value));
        internal void WriteString(string text) => WriteBytes(ByteString.CopyFrom(Encoding.UTF8.GetBytes(text)));
        internal void WriteBytes(ByteString value) { WriteUInt64((ulong)value.Length); var bytes = value.ToByteArray(); _stream.Write(bytes, 0, bytes.Length); }
        internal void Flush() => _stream.Flush();
        public void Dispose() { Flush(); }
    }
    internal sealed class CodedInputStream : IDisposable
    {
        private readonly Stream _stream;
        private uint _tag;
        internal CodedInputStream(Stream stream) { _stream = stream; }
        internal bool IsAtEnd => _stream.Position == _stream.Length;
        private ulong Varint()
        {
            ulong value = 0;
            for (var shift = 0; shift < 70; shift += 7)
            {
                var b = _stream.ReadByte();
                if (b < 0) throw new EndOfStreamException("Truncated Lens response.");
                if (shift == 63 && b > 1) throw new InvalidDataException("Invalid Lens varint.");
                value |= (ulong)(b & 127) << shift;
                if (b < 128) return value;
            }
            throw new InvalidDataException("Invalid Lens varint.");
        }
        internal uint ReadTag()
        {
            var value = Varint();
            if (value > uint.MaxValue || (value >> 3) == 0) throw new InvalidDataException("Invalid Lens field.");
            return _tag = (uint)value;
        }
        private byte[] ReadExact(int count)
        {
            if (count < 0 || count > 32 * 1024 * 1024 || count > _stream.Length - _stream.Position) throw new InvalidDataException("Invalid Lens field length.");
            var bytes = new byte[count];
            var offset = 0;
            while (offset < count) { var read = _stream.Read(bytes, offset, count - offset); if (read == 0) throw new EndOfStreamException(); offset += read; }
            return bytes;
        }
        private void Expect(int type) { if ((_tag & 7) != type) throw new InvalidDataException("Unexpected Lens field type."); }
        internal ByteString ReadBytes() { Expect(2); var length = Varint(); if (length > int.MaxValue) throw new InvalidDataException("Invalid Lens length."); return ByteString.CopyFrom(ReadExact((int)length)); }
        internal string ReadString() => new UTF8Encoding(false, true).GetString(ReadBytes().ToByteArray());
        internal int ReadInt32() { Expect(0); return unchecked((int)Varint()); }
        internal float ReadFloat() { Expect(5); var bytes = ReadExact(4); if (!BitConverter.IsLittleEndian) Array.Reverse(bytes); return BitConverter.ToSingle(bytes, 0); }
        internal void SkipLastField()
        {
            switch (_tag & 7)
            {
                case 0: Varint(); break;
                case 1: ReadExact(8); break;
                case 2: ReadBytes(); break;
                case 5: ReadExact(4); break;
                default: throw new InvalidDataException("Unsupported Lens wire type.");
            }
        }
        public void Dispose() { }
    }
}
