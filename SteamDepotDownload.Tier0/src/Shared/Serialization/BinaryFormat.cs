using SteamDepotDownload.Tier0.Core.Serialization;

namespace SteamDepotDownload.Tier0.Shared.Serialization;

public static class BinaryFormat
{
    private static readonly IBinarySerializer Serializer = new CBinarySerializer();

    public static void Serialize<T>(Stream stream, T value) => Serializer.Serialize(stream, value);

    public static T? Deserialize<T>(Stream stream) => Serializer.Deserialize<T>(stream);

    public static byte[] ToBytes<T>(T value) => Serializer.ToBytes(value);

    public static T? FromBytes<T>(ReadOnlySpan<byte> bytes) => Serializer.FromBytes<T>(bytes);
}
