namespace SteamDepotDownload.Steam.Shared.Depot;

/// <summary>
/// A depot as advertised by an app's product info, after branch/manifest resolution.
/// </summary>
public sealed record DepotInfo
{
    public required uint DepotId { get; init; }

    public required uint AppId { get; init; }

    public string? Name { get; init; }

    public ulong ManifestId { get; init; } = DepotConstants.InvalidManifestId;

    public string Branch { get; init; } = DepotConstants.PublicBranch;

    public ulong SizeOnDisk { get; init; }

    public ulong DownloadSize { get; init; }

    public string? Os { get; init; }

    public string? Arch { get; init; }

    public string? Language { get; init; }

    public bool LowViolence { get; init; }

    public bool SharedInstall { get; init; }
}
