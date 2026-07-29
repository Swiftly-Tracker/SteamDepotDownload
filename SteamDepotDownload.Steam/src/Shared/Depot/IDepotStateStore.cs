namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed record DepotInstallRecord
{
    public required uint DepotId { get; init; }

    public required ulong ManifestId { get; init; }

    public uint AppId { get; init; }

    public string Branch { get; init; } = DepotConstants.PublicBranch;

    public uint BuildId { get; init; }

    public string? Name { get; init; }

    public ulong SizeOnDisk { get; init; }

    public int FileCount { get; init; }

    public DateTimeOffset UpdatedUtc { get; init; }
}

public interface IDepotStateStore
{
    ulong GetInstalledManifest(uint depotId);

    void SetInstalledManifest(uint depotId, ulong manifestId);

    void Remove(uint depotId);

    void Save();

    void Record(DepotInstallRecord record)
        => SetInstalledManifest(record.DepotId, record.ManifestId);

    DepotInstallRecord? GetRecord(uint depotId)
    {
        var manifestId = GetInstalledManifest(depotId);

        return manifestId == DepotConstants.InvalidManifestId
            ? null
            : new DepotInstallRecord { DepotId = depotId, ManifestId = manifestId };
    }

    IReadOnlyList<DepotInstallRecord> Records => [];
}
