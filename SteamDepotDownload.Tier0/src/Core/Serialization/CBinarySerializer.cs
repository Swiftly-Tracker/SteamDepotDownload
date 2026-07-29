using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Tier0.Core.Serialization;

[ExposeInterface(InterfaceNames.BinarySerializer)]
internal sealed class CBinarySerializer : IBinarySerializer
{
    private static readonly byte[] Magic = "SDDB"u8.ToArray();

    private const byte FormatVersion = 1;

    public void Serialize<T>(Stream stream, T value)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "The binary format has no encoding for a null document.");
        }

        stream.Write(Magic);
        stream.WriteByte(FormatVersion);

        CBinaryTypePlan<T>.Instance.Write(new CBinaryWriter(stream), value);
    }

    public T? Deserialize<T>(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> header = stackalloc byte[Magic.Length + 1];

        try
        {
            stream.ReadExactly(header);
        }
        catch (EndOfStreamException ex)
        {
            throw new BinaryFormatException("The payload is too short to hold a header.", ex);
        }

        if (!header[..Magic.Length].SequenceEqual(Magic))
        {
            throw new BinaryFormatException("The payload does not start with the expected magic.");
        }

        if (header[Magic.Length] != FormatVersion)
        {
            throw new BinaryFormatException(
                $"The payload is format version {header[Magic.Length]}, and this build reads version {FormatVersion}.");
        }

        try
        {
            return CBinaryTypePlan<T>.Instance.Read(new CBinaryReader(stream));
        }
        catch (EndOfStreamException ex)
        {
            throw new BinaryFormatException("The payload ends in the middle of a value.", ex);
        }
    }

    public byte[] ToBytes<T>(T value)
    {
        using var buffer = new MemoryStream();

        Serialize(buffer, value);

        return buffer.ToArray();
    }

    public T? FromBytes<T>(ReadOnlySpan<byte> bytes)
    {
        using var buffer = new MemoryStream(bytes.ToArray(), writable: false);

        return Deserialize<T>(buffer);
    }
}
