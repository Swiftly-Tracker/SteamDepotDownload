namespace SteamDepotDownload.Steam.Shared.Session;

public interface ISteamClientFactory
{
    Task<ISteamSession> ConnectAsync(SteamCredentials credentials,
        SteamSessionOptions? options = null, CancellationToken ct = default);
}
