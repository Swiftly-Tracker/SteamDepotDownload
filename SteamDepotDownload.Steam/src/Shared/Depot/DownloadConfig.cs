namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed record DownloadConfig
{
    public string? InstallDirectory { get; init; }

    public uint CellId { get; init; }

    public int MaxDownloads { get; init; } = 8;

    public bool VerifyAll { get; init; }

    public bool ManifestOnly { get; init; }

    public FileFilter? FileFilter { get; init; }

    public IDepotStateStore? StateStore { get; init; }

    public bool RemoveUnusedFiles { get; init; }
}
