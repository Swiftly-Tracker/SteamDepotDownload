using System.Buffers.Binary;
using System.Text;
using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Tier0.Core.Serialization;

internal sealed class CBinaryReader
{
    private const int MaxDepth = 64;

    private readonly Stream _stream;
    private readonly byte[] _scratch = new byte[16];

    private int _depth;

    internal CBinaryReader(Stream stream) => _stream = stream;

    internal void Enter()
    {
        if (++_depth > MaxDepth)
        {
            throw new BinaryFormatException($"Nesting is deeper than the {MaxDepth} level limit.");
        }
    }

    internal void Leave() => _depth--;

    internal bool ReadKey(out int tag, out CBinaryWireType wire)
    {
        var key = ReadVarUInt();

        tag = (int)(key >> 3);
        wire = (CBinaryWireType)(byte)(key & 0x7);

        return tag != 0;
    }

    internal byte ReadByteOrThrow()
    {
        var value = _stream.ReadByte();

        if (value < 0)
        {
            throw new BinaryFormatException("The payload ends in the middle of a value.");
        }

        return (byte)value;
    }

    internal ulong ReadVarUInt()
    {
        var result = 0UL;
        var shift = 0;

        while (shift <= 63)
        {
            var current = ReadByteOrThrow();
            result |= (ulong)(current & 0x7F) << shift;

            if ((current & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new BinaryFormatException("A varint runs longer than 64 bits.");
    }

    internal long ReadVarInt()
    {
        var raw = ReadVarUInt();
        return (long)(raw >> 1) ^ -(long)(raw & 1);
    }

    internal uint ReadFixed32()
    {
        _stream.ReadExactly(_scratch, 0, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(_scratch);
    }

    internal ulong ReadFixed64()
    {
        _stream.ReadExactly(_scratch, 0, 8);
        return BinaryPrimitives.ReadUInt64LittleEndian(_scratch);
    }

    internal byte[] ReadBytes()
    {
        var length = ReadLength();

        if (length == 0)
        {
            return [];
        }

        var buffer = new byte[length];
        _stream.ReadExactly(buffer, 0, length);

        return buffer;
    }

    internal string ReadString() => Encoding.UTF8.GetString(ReadBytes());

    internal int ReadCount(int bytesPerElement)
    {
        var count = ReadVarUInt();

        if (count > int.MaxValue)
        {
            throw new BinaryFormatException($"An element count of {count} is not readable.");
        }

        Require((long)count * Math.Max(1, bytesPerElement));

        return (int)count;
    }

    internal void Skip(CBinaryWireType wire)
    {
        switch (wire)
        {
            case CBinaryWireType.VarInt:
                ReadVarUInt();
                break;

            case CBinaryWireType.Fixed32:
                _stream.ReadExactly(_scratch, 0, 4);
                break;

            case CBinaryWireType.Fixed64:
                _stream.ReadExactly(_scratch, 0, 8);
                break;

            case CBinaryWireType.Bytes:
                SkipBytes(ReadLength());
                break;

            case CBinaryWireType.Object:
                Enter();
                while (ReadKey(out _, out var member))
                {
                    Skip(member);
                }

                Leave();
                break;

            case CBinaryWireType.Array:
                {
                    var count = ReadCount(bytesPerElement: 1);
                    var element = (CBinaryWireType)ReadByteOrThrow();

                    for (var i = 0; i < count; i++)
                    {
                        Skip(element);
                    }

                    break;
                }

            case CBinaryWireType.Map:
                {
                    var count = ReadCount(bytesPerElement: 2);
                    var key = (CBinaryWireType)ReadByteOrThrow();
                    var value = (CBinaryWireType)ReadByteOrThrow();

                    for (var i = 0; i < count; i++)
                    {
                        Skip(key);
                        Skip(value);
                    }

                    break;
                }

            default:
                throw new BinaryFormatException($"Wire type {(byte)wire} is not one this format defines.");
        }
    }

    private int ReadLength()
    {
        var length = ReadVarUInt();

        if (length > int.MaxValue)
        {
            throw new BinaryFormatException($"A length of {length} is not readable.");
        }

        Require((long)length);

        return (int)length;
    }

    private void Require(long bytes)
    {
        if (!_stream.CanSeek)
        {
            return;
        }

        var remaining = _stream.Length - _stream.Position;

        if (bytes > remaining)
        {
            throw new BinaryFormatException(
                $"The payload claims {bytes} more bytes but only {remaining} are left.");
        }
    }

    private void SkipBytes(int count)
    {
        if (_stream.CanSeek)
        {
            _stream.Seek(count, SeekOrigin.Current);
            return;
        }

        while (count > 0)
        {
            var read = _stream.Read(_scratch, 0, Math.Min(count, _scratch.Length));

            if (read <= 0)
            {
                throw new BinaryFormatException("The payload ends in the middle of a value.");
            }

            count -= read;
        }
    }
}
