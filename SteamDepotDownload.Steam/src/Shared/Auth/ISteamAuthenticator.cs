namespace SteamDepotDownload.Steam.Shared.Auth;

public interface ISteamAuthenticator
{
    Task<string> GetTwoFactorCodeAsync(bool previousAttemptIncorrect, CancellationToken ct);

    Task<string> GetEmailCodeAsync(string? email, bool previousAttemptIncorrect, CancellationToken ct);

    Task<bool> AcceptDeviceConfirmationAsync(CancellationToken ct);

    Task<string> GetPasswordAsync(string username, CancellationToken ct);

    Task OnQrChallengeUrlAsync(string challengeUrl, CancellationToken ct);
}
