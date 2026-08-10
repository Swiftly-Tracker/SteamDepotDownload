namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed record DownloadConfig
{
    public string? InstallDirectory { get; init; }

    public uint CellId { get; init; }

    public int MaxDownloads { get; init; } = 32;

    public bool VerifyAll { get; init; }

    public bool ManifestOnly { get; init; }

    public FileFilter? FileFilter { get; init; }

    public IDepotStateStore? StateStore { get; init; }

    public bool RemoveUnusedFiles { get; init; }
}
