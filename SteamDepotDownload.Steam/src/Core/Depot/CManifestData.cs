using SteamDepotDownload.Tier0.Shared.Serialization;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

[BinaryContract]
internal sealed class CManifestData
{
    public const int CurrentVersion = 1;

    [BinaryMember(1)]
    public int Version { get; set; } = CurrentVersion;

    [BinaryMember(2)]
    public uint DepotId { get; set; }

    [BinaryMember(3)]
    public ulong ManifestGid { get; set; }

    [BinaryMember(4)]
    public DateTime CreationTime { get; set; }

    [BinaryMember(5)]
    public ulong TotalUncompressedSize { get; set; }

    [BinaryMember(6)]
    public ulong TotalCompressedSize { get; set; }

    /// <remarks>
    /// Only ever false on a cached manifest: the download path decrypts before it saves. Kept so a
    /// file that somehow holds encrypted names can be spotted and refetched rather than trusted.
    /// </remarks>
    [BinaryMember(7)]
    public bool FilenamesEncrypted { get; set; }

    [BinaryMember(8)]
    public List<CManifestFile> Entries { get; set; } = [];

    public List<DepotManifest.FileData> Files
        => field ??= Entries.ConvertAll(entry => entry.ToFileData());

    internal static CManifestData FromSteamKit(DepotManifest manifest) => new()
    {
        DepotId = manifest.DepotID,
        ManifestGid = manifest.ManifestGID,
        CreationTime = manifest.CreationTime,
        TotalUncompressedSize = manifest.TotalUncompressedSize,
        TotalCompressedSize = manifest.TotalCompressedSize,
        FilenamesEncrypted = manifest.FilenamesEncrypted,
        Entries = (manifest.Files ?? []).ConvertAll(CManifestFile.FromSteamKit),
    };
}

[BinaryContract]
internal sealed class CManifestFile
{
    [BinaryMember(1)]
    public string Name { get; set; } = string.Empty;

    [BinaryMember(2)]
    public byte[] NameHash { get; set; } = [];

    [BinaryMember(3)]
    public uint Flags { get; set; }

    [BinaryMember(4)]
    public ulong Size { get; set; }

    [BinaryMember(5)]
    public byte[] Hash { get; set; } = [];

    [BinaryMember(6)]
    public string? LinkTarget { get; set; }

    [BinaryMember(7)]
    public List<CManifestChunk> Chunks { get; set; } = [];

    internal static CManifestFile FromSteamKit(DepotManifest.FileData file) => new()
    {
        Name = file.FileName,
        NameHash = file.FileNameHash ?? [],
        Flags = (uint)file.Flags,
        Size = file.TotalSize,
        Hash = file.FileHash ?? [],
        LinkTarget = string.IsNullOrEmpty(file.LinkTarget) ? null : file.LinkTarget,
        Chunks = file.Chunks.ConvertAll(CManifestChunk.FromSteamKit),
    };

    internal DepotManifest.FileData ToFileData()
    {
        var file = new DepotManifest.FileData(Name, NameHash, (EDepotFileFlag)Flags, Size, Hash,
            LinkTarget ?? string.Empty, encrypted: false, Chunks.Count);

        foreach (var chunk in Chunks)
        {
            file.Chunks.Add(chunk.ToChunkData());
        }

        return file;
    }
}

[BinaryContract]
internal sealed class CManifestChunk
{
    [BinaryMember(1)]
    public byte[] Id { get; set; } = [];

    [BinaryMember(2)]
    public uint Checksum { get; set; }

    [BinaryMember(3)]
    public ulong Offset { get; set; }

    [BinaryMember(4)]
    public uint CompressedLength { get; set; }

    [BinaryMember(5)]
    public uint UncompressedLength { get; set; }

    internal static CManifestChunk FromSteamKit(DepotManifest.ChunkData chunk) => new()
    {
        Id = chunk.ChunkID ?? [],
        Checksum = chunk.Checksum,
        Offset = chunk.Offset,
        CompressedLength = chunk.CompressedLength,
        UncompressedLength = chunk.UncompressedLength,
    };

    internal DepotManifest.ChunkData ToChunkData()
        => new(Id, Checksum, Offset, CompressedLength, UncompressedLength);
}
