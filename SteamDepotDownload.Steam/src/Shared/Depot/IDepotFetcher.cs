namespace SteamDepotDownload.Steam.Shared.Depot;

public interface IDepotFetcher
{
    DownloadConfig Config { get; }

    Task<DownloadResult> DownloadAppAsync(AppDownloadRequest request,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);

    Task<DownloadResult> DownloadPubfileAsync(ulong publishedFileId,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);

    Task<DownloadResult> DownloadUgcAsync(uint appId, ulong ugcId,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);

    Task<IReadOnlyList<DepotInfo>> ResolveDepotsAsync(AppDownloadRequest request,
        CancellationToken ct = default);

    Task<string> DumpManifestAsync(uint appId, uint depotId, ulong manifestId, string branch,
        CancellationToken ct = default);
}
