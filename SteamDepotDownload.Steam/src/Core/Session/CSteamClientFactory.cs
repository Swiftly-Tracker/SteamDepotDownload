using SteamDepotDownload.Steam.Shared.Interfaces;
using SteamDepotDownload.Steam.Shared.Session;
using SteamDepotDownload.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Steam.Core.Session;

[ExposeInterface(SteamInterfaceNames.SteamClientFactory)]
internal sealed class CSteamClientFactory : ISteamClientFactory
{
    public async Task<ISteamSession> ConnectAsync(SteamCredentials credentials,
        SteamSessionOptions? options = null, CancellationToken ct = default)
    {
        var session = new CSteamSession(credentials, options ?? new SteamSessionOptions());

        try
        {
            await session.ConnectAsync(ct).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
