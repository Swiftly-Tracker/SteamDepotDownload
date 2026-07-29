using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Auth;

namespace SteamDepotDownload.Steam.Core.Auth;

internal sealed class CTerminalAuthenticator : ISteamAuthenticator
{
    private static readonly Lock _lock = new();
    private static TaskCompletionSource<string>? _pending;
    private static string? _pendingPrompt;

    private readonly bool _preferTwoFactorCode;

    internal CTerminalAuthenticator(bool preferTwoFactorCode = false)
    {
        _preferTwoFactorCode = preferTwoFactorCode;
    }

    internal static string? PendingPrompt
    {
        get
        {
            lock (_lock)
            {
                return _pendingPrompt;
            }
        }
    }

    internal static bool TrySupply(string value)
    {
        TaskCompletionSource<string>? pending;

        lock (_lock)
        {
            pending = _pending;
            _pending = null;
            _pendingPrompt = null;
        }

        return pending != null && pending.TrySetResult(value);
    }

    internal static void CancelPending()
    {
        TaskCompletionSource<string>? pending;

        lock (_lock)
        {
            pending = _pending;
            _pending = null;
            _pendingPrompt = null;
        }

        pending?.TrySetCanceled();
    }

    public Task<string> GetTwoFactorCodeAsync(bool previousAttemptIncorrect, CancellationToken ct)
    {
        if (previousAttemptIncorrect)
        {
            CSteamLog.Warning(CSteamLog.Steam, "That Steam Guard code was not accepted.");
        }

        return AskAsync("Steam Guard code from your mobile authenticator", ct);
    }

    public Task<string> GetEmailCodeAsync(string? email, bool previousAttemptIncorrect, CancellationToken ct)
    {
        if (previousAttemptIncorrect)
        {
            CSteamLog.Warning(CSteamLog.Steam, "That code was not accepted.");
        }

        var where = string.IsNullOrEmpty(email) ? "your email" : email;
        return AskAsync($"Steam Guard code sent to {where}", ct);
    }

    public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken ct)
    {
        if (!_preferTwoFactorCode)
        {
            CSteamLog.Msg(CSteamLog.Steam, "Approve this login in the Steam mobile app.");
        }

        return Task.FromResult(!_preferTwoFactorCode);
    }

    public Task<string> GetPasswordAsync(string username, CancellationToken ct)
        => AskAsync($"password for {username}", ct);

    public Task OnQrChallengeUrlAsync(string challengeUrl, CancellationToken ct)
    {
        foreach (var line in CQrRenderer.Render(challengeUrl))
        {
            CSteamLog.Msg(CSteamLog.Steam, line);
        }

        CSteamLog.Msg(CSteamLog.Steam, "Scan the code with the Steam mobile app to log in.");
        return Task.CompletedTask;
    }

    private static Task<string> AskAsync(string what, CancellationToken ct)
    {
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            _pending?.TrySetCanceled();
            _pending = source;
            _pendingPrompt = what;
        }

        CSteamLog.Msg(CSteamLog.Steam, $"Login needs the {what}. Enter it with: steam_code <value>");

        ct.Register(static state =>
        {
            var pending = (TaskCompletionSource<string>)state!;
            pending.TrySetCanceled();
        }, source);

        return source.Task;
    }
}
