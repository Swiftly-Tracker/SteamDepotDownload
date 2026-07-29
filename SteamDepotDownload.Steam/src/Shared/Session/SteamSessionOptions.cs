using SteamDepotDownload.Steam.Shared.Auth;

namespace SteamDepotDownload.Steam.Shared.Session;

public sealed record SteamSessionOptions
{
    public IAccountSettingsStore? AccountStore { get; init; }

    public ISteamAuthenticator? Authenticator { get; init; }

    public uint? CellIdOverride { get; init; }

    public bool UseLancache { get; init; }

    public bool Debug { get; init; }

    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxReconnectAttempts { get; init; } = 3;
}
