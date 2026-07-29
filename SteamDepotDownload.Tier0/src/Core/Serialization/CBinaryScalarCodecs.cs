using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Tier0.Core.Serialization;

internal sealed class CBoolCodec : ICBinaryCodec<bool>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, bool value) => writer.WriteVarUInt(value ? 1UL : 0UL);

    public bool Read(CBinaryReader reader) => reader.ReadVarUInt() != 0;
}

internal sealed class CByteCodec : ICBinaryCodec<byte>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, byte value) => writer.WriteVarUInt(value);

    public byte Read(CBinaryReader reader) => (byte)reader.ReadVarUInt();
}

internal sealed class CSByteCodec : ICBinaryCodec<sbyte>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, sbyte value) => writer.WriteVarInt(value);

    public sbyte Read(CBinaryReader reader) => (sbyte)reader.ReadVarInt();
}

internal sealed class CInt16Codec : ICBinaryCodec<short>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, short value) => writer.WriteVarInt(value);

    public short Read(CBinaryReader reader) => (short)reader.ReadVarInt();
}

internal sealed class CUInt16Codec : ICBinaryCodec<ushort>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, ushort value) => writer.WriteVarUInt(value);

    public ushort Read(CBinaryReader reader) => (ushort)reader.ReadVarUInt();
}

internal sealed class CInt32Codec : ICBinaryCodec<int>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, int value) => writer.WriteVarInt(value);

    public int Read(CBinaryReader reader) => (int)reader.ReadVarInt();
}

internal sealed class CUInt32Codec : ICBinaryCodec<uint>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, uint value) => writer.WriteVarUInt(value);

    public uint Read(CBinaryReader reader) => (uint)reader.ReadVarUInt();
}

internal sealed class CInt64Codec : ICBinaryCodec<long>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, long value) => writer.WriteVarInt(value);

    public long Read(CBinaryReader reader) => reader.ReadVarInt();
}

internal sealed class CUInt64Codec : ICBinaryCodec<ulong>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, ulong value) => writer.WriteVarUInt(value);

    public ulong Read(CBinaryReader reader) => reader.ReadVarUInt();
}

internal sealed class CCharCodec : ICBinaryCodec<char>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, char value) => writer.WriteVarUInt(value);

    public char Read(CBinaryReader reader) => (char)reader.ReadVarUInt();
}

internal sealed class CSingleCodec : ICBinaryCodec<float>
{
    public CBinaryWireType Wire => CBinaryWireType.Fixed32;

    public void Write(CBinaryWriter writer, float value)
        => writer.WriteFixed32(BitConverter.SingleToUInt32Bits(value));

    public float Read(CBinaryReader reader) => BitConverter.UInt32BitsToSingle(reader.ReadFixed32());
}

internal sealed class CDoubleCodec : ICBinaryCodec<double>
{
    public CBinaryWireType Wire => CBinaryWireType.Fixed64;

    public void Write(CBinaryWriter writer, double value)
        => writer.WriteFixed64(BitConverter.DoubleToUInt64Bits(value));

    public double Read(CBinaryReader reader) => BitConverter.UInt64BitsToDouble(reader.ReadFixed64());
}

internal sealed class CDecimalCodec : ICBinaryCodec<decimal>
{
    public CBinaryWireType Wire => CBinaryWireType.Bytes;

    public void Write(CBinaryWriter writer, decimal value)
    {
        Span<byte> buffer = stackalloc byte[16];
        Span<int> bits = stackalloc int[4];

        decimal.GetBits(value, bits);

        for (var i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer[(i * 4)..], bits[i]);
        }

        writer.WriteBytes(buffer);
    }

    public decimal Read(CBinaryReader reader)
    {
        var raw = reader.ReadBytes();

        if (raw.Length != 16)
        {
            throw new BinaryFormatException($"A decimal needs 16 bytes, got {raw.Length}.");
        }

        Span<int> bits = stackalloc int[4];

        for (var i = 0; i < 4; i++)
        {
            bits[i] = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(i * 4));
        }

        return new decimal(bits);
    }
}

internal sealed class CStringCodec : ICBinaryCodec<string>
{
    public CBinaryWireType Wire => CBinaryWireType.Bytes;

    public void Write(CBinaryWriter writer, string value) => writer.WriteString(value);

    public string Read(CBinaryReader reader) => reader.ReadString();
}

internal sealed class CByteArrayCodec : ICBinaryCodec<byte[]>
{
    public CBinaryWireType Wire => CBinaryWireType.Bytes;

    public void Write(CBinaryWriter writer, byte[] value) => writer.WriteBytes(value);

    public byte[] Read(CBinaryReader reader) => reader.ReadBytes();
}

internal sealed class CGuidCodec : ICBinaryCodec<Guid>
{
    public CBinaryWireType Wire => CBinaryWireType.Bytes;

    public void Write(CBinaryWriter writer, Guid value)
    {
        Span<byte> buffer = stackalloc byte[16];
        value.TryWriteBytes(buffer);
        writer.WriteBytes(buffer);
    }

    public Guid Read(CBinaryReader reader)
    {
        var raw = reader.ReadBytes();

        if (raw.Length != 16)
        {
            throw new BinaryFormatException($"A Guid needs 16 bytes, got {raw.Length}.");
        }

        return new Guid(raw);
    }
}

internal sealed class CTimeSpanCodec : ICBinaryCodec<TimeSpan>
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public void Write(CBinaryWriter writer, TimeSpan value) => writer.WriteVarInt(value.Ticks);

    public TimeSpan Read(CBinaryReader reader) => new(reader.ReadVarInt());
}

internal sealed class CDateTimeCodec : ICBinaryCodec<DateTime>
{
    public CBinaryWireType Wire => CBinaryWireType.Bytes;

    public void Write(CBinaryWriter writer, DateTime value)
    {
        Span<byte> buffer = stackalloc byte[9];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, value.Ticks);
        buffer[8] = (byte)value.Kind;

        writer.WriteBytes(buffer);
    }

    public DateTime Read(CBinaryReader reader)
    {
        var raw = reader.ReadBytes();

        if (raw.Length != 9)
        {
            throw new BinaryFormatException($"A DateTime needs 9 bytes, got {raw.Length}.");
        }

        var ticks = BinaryPrimitives.ReadInt64LittleEndian(raw);
        var kind = (DateTimeKind)raw[8];

        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || kind > DateTimeKind.Local)
        {
            throw new BinaryFormatException("A DateTime holds a tick count or kind outside the legal range.");
        }

        return new DateTime(ticks, kind);
    }
}

internal sealed class CDateTimeOffsetCodec : ICBinaryCodec<DateTimeOffset>
{
    public CBinaryWireType Wire => CBinaryWireType.Bytes;

    public void Write(CBinaryWriter writer, DateTimeOffset value)
    {
        Span<byte> buffer = stackalloc byte[10];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, value.Ticks);
        BinaryPrimitives.WriteInt16LittleEndian(buffer[8..], (short)(value.Offset.Ticks / TimeSpan.TicksPerMinute));

        writer.WriteBytes(buffer);
    }

    public DateTimeOffset Read(CBinaryReader reader)
    {
        var raw = reader.ReadBytes();

        if (raw.Length != 10)
        {
            throw new BinaryFormatException($"A DateTimeOffset needs 10 bytes, got {raw.Length}.");
        }

        var ticks = BinaryPrimitives.ReadInt64LittleEndian(raw);
        var offset = TimeSpan.FromMinutes(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(8)));

        try
        {
            return new DateTimeOffset(ticks, offset);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new BinaryFormatException("A DateTimeOffset holds a tick count or offset outside the legal range.", ex);
        }
    }
}

internal sealed class CEnumCodec<T> : ICBinaryCodec<T>
    where T : unmanaged, Enum
{
    public CBinaryWireType Wire => CBinaryWireType.VarInt;

    public unsafe void Write(CBinaryWriter writer, T value)
    {
        ulong raw = sizeof(T) switch
        {
            1 => Unsafe.As<T, byte>(ref value),
            2 => Unsafe.As<T, ushort>(ref value),
            4 => Unsafe.As<T, uint>(ref value),
            _ => Unsafe.As<T, ulong>(ref value),
        };

        writer.WriteVarUInt(raw);
    }

    public unsafe T Read(CBinaryReader reader)
    {
        var raw = reader.ReadVarUInt();
        var result = default(T);

        switch (sizeof(T))
        {
            case 1:
                Unsafe.As<T, byte>(ref result) = (byte)raw;
                break;

            case 2:
                Unsafe.As<T, ushort>(ref result) = (ushort)raw;
                break;

            case 4:
                Unsafe.As<T, uint>(ref result) = (uint)raw;
                break;

            default:
                Unsafe.As<T, ulong>(ref result) = raw;
                break;
        }

        return result;
    }
}
