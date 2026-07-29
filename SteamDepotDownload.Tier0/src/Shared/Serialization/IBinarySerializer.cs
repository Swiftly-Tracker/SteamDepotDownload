namespace SteamDepotDownload.Tier0.Shared.Serialization;

public interface IBinarySerializer
{
    void Serialize<T>(Stream stream, T value);

    T? Deserialize<T>(Stream stream);

    byte[] ToBytes<T>(T value);

    T? FromBytes<T>(ReadOnlySpan<byte> bytes);
}
