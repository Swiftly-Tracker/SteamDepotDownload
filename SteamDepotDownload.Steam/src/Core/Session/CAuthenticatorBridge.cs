using SteamDepotDownload.Steam.Shared.Auth;
using SteamKit2.Authentication;

namespace SteamDepotDownload.Steam.Core.Session;

internal sealed class CAuthenticatorBridge : IAuthenticator
{
    private readonly ISteamAuthenticator _authenticator;
    private readonly CancellationToken _ct;

    internal CAuthenticatorBridge(ISteamAuthenticator authenticator, CancellationToken ct)
    {
        _authenticator = authenticator;
        _ct = ct;
    }

    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        => _authenticator.GetTwoFactorCodeAsync(previousCodeWasIncorrect, _ct);

    public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
        => _authenticator.GetEmailCodeAsync(email, previousCodeWasIncorrect, _ct);

    public Task<bool> AcceptDeviceConfirmationAsync()
        => _authenticator.AcceptDeviceConfirmationAsync(_ct);
}
