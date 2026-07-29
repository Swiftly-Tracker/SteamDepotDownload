using SteamDepotDownload.Steam.Shared.Depot;

namespace SteamDepotDownload.Steam.Shared.Session;

public interface ISteamSession : IAsyncDisposable
{
    bool IsLoggedOn { get; }

    string? AccountName { get; }

    ulong SteamId { get; }

    uint CellId { get; }

    bool IsAnonymous { get; }

    IDepotFetcher CreateDownloader(DownloadConfig config);

    Task<AppInfo?> GetAppInfoAsync(uint appId, CancellationToken ct = default);

    Task<IReadOnlyList<uint>> GetLicensedPackagesAsync(CancellationToken ct = default);

    Task DisconnectAsync();
}
