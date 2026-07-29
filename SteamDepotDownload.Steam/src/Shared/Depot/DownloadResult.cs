namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed record DepotDownloadSummary
{
    public required uint DepotId { get; init; }

    public required ulong ManifestId { get; init; }

    public required string InstallDirectory { get; init; }

    public ulong BytesDownloaded { get; init; }

    public ulong BytesTotal { get; init; }

    public int FilesDownloaded { get; init; }

    public int FilesSkipped { get; init; }

    public bool AlreadyInstalled { get; init; }

    public string? ManifestDumpPath { get; init; }
}

public sealed record DownloadResult
{
    public required IReadOnlyList<DepotDownloadSummary> Depots { get; init; }

    public ulong BytesDownloaded { get; init; }

    public ulong BytesTotal { get; init; }

    public TimeSpan Elapsed { get; init; }
}
