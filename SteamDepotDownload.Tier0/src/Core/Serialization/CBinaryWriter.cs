using System.Buffers.Binary;
using System.Text;

namespace SteamDepotDownload.Tier0.Core.Serialization;

internal sealed class CBinaryWriter
{
    private readonly Stream _stream;
    private readonly byte[] _scratch = new byte[16];

    internal CBinaryWriter(Stream stream) => _stream = stream;

    internal void WriteKey(int tag, CBinaryWireType wire)
        => WriteVarUInt(((ulong)tag << 3) | (byte)wire);

    internal void WriteEndObject() => _stream.WriteByte(0);

    internal void WriteWire(CBinaryWireType wire) => _stream.WriteByte((byte)wire);

    internal void WriteVarUInt(ulong value)
    {
        var count = 0;

        while (value >= 0x80)
        {
            _scratch[count++] = (byte)(value | 0x80);
            value >>= 7;
        }

        _scratch[count++] = (byte)value;
        _stream.Write(_scratch, 0, count);
    }

    internal void WriteVarInt(long value) => WriteVarUInt((ulong)((value << 1) ^ (value >> 63)));

    internal void WriteFixed32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_scratch, value);
        _stream.Write(_scratch, 0, 4);
    }

    internal void WriteFixed64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_scratch, value);
        _stream.Write(_scratch, 0, 8);
    }

    internal void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteVarUInt((ulong)value.Length);
        _stream.Write(value);
    }

    internal void WriteString(string value)
    {
        var count = Encoding.UTF8.GetByteCount(value);
        WriteVarUInt((ulong)count);

        if (count == 0)
        {
            return;
        }

        var buffer = count <= 256 ? stackalloc byte[count] : new byte[count];
        Encoding.UTF8.GetBytes(value, buffer);
        _stream.Write(buffer);
    }
}
