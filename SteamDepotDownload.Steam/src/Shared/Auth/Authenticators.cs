using SteamDepotDownload.Steam.Core.Auth;

namespace SteamDepotDownload.Steam.Shared.Auth;

public static class Authenticators
{
    public static ISteamAuthenticator CreateConsole(bool preferTwoFactorCode = false)
        => new CConsoleAuthenticator(preferTwoFactorCode);

    public static ISteamAuthenticator CreateTerminal(bool preferTwoFactorCode = false)
        => new CTerminalAuthenticator(preferTwoFactorCode);
}

public static class AccountSettingsStore
{
    public static string DefaultPath => CFileAccountSettingsStore.DefaultPath;

    public static IAccountSettingsStore CreateDefault() => new CFileAccountSettingsStore();

    public static IAccountSettingsStore CreateAt(string path) => new CFileAccountSettingsStore(path);
}
