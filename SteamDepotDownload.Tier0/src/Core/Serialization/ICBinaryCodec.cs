namespace SteamDepotDownload.Tier0.Core.Serialization;

internal interface ICBinaryCodec<T>
{
    CBinaryWireType Wire { get; }

    void Write(CBinaryWriter writer, T value);

    T Read(CBinaryReader reader);
}

internal delegate void CBinaryRefReader<T>(ref T instance, CBinaryReader reader);
