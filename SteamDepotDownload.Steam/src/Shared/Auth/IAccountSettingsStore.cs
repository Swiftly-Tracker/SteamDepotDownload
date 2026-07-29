namespace SteamDepotDownload.Steam.Shared.Auth;

public interface IAccountSettingsStore
{
    string? GetRefreshToken(string account);

    void SetRefreshToken(string account, string token);

    void RemoveRefreshToken(string account);

    string? GetGuardData(string account);

    void SetGuardData(string account, string data);

    int GetContentServerPenalty(string host);

    void SetContentServerPenalty(string host, int penalty);

    void Save();
}
