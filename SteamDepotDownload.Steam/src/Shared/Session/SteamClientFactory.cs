using SteamDepotDownload.Steam.Core.Session;

namespace SteamDepotDownload.Steam.Shared.Session;

public static class SteamClientFactory
{
    public static ISteamClientFactory Create() => new CSteamClientFactory();
}
