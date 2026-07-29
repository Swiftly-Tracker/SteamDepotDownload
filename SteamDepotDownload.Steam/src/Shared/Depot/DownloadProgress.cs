namespace SteamDepotDownload.Steam.Shared.Depot;

public readonly record struct DownloadProgress
{
    public uint DepotId { get; init; }

    public ulong BytesDownloaded { get; init; }

    public ulong BytesTotal { get; init; }

    public string? CurrentFile { get; init; }

    public string? Stage { get; init; }

    public double Fraction => BytesTotal == 0 ? 0d : Math.Clamp((double)BytesDownloaded / BytesTotal, 0d, 1d);
}
