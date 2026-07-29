using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Session;

namespace SteamDepotDownload.Steam.Core.Session;

internal static class CSessionHolder
{
    private static readonly Lock _lock = new();
    private static ISteamSession? _session;
    private static TaskCompletionSource? _pending;

    internal static ISteamSession? Current
    {
        get
        {
            lock (_lock)
            {
                return _session;
            }
        }
    }

    internal static TaskCompletionSource BeginLogin()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            _pending = gate;
        }

        return gate;
    }

    internal static async Task<ISteamSession> RequireAsync(CancellationToken ct)
    {
        Task? pending;

        lock (_lock)
        {
            if (_session != null)
            {
                return _session;
            }

            pending = _pending?.Task;
        }

        if (pending != null)
        {
            await pending.WaitAsync(ct).ConfigureAwait(false);
        }

        return Current ?? throw new DepotDownloadException(
            "Not logged in. Run steam_login_anonymous, steam_login <user>, or steam_login_qr first.");
    }

    internal static async Task LoginAsync(SteamCredentials credentials, SteamSessionOptions options,
        CancellationToken ct)
    {
        await CloseCurrentAsync().ConfigureAwait(false);

        var factory = new CSteamClientFactory();
        var session = await factory.ConnectAsync(credentials, options, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _session = session;
        }
    }

    internal static async Task LogoutAsync()
    {
        TaskCompletionSource? pending;

        lock (_lock)
        {
            pending = _pending;
            _pending = null;
        }

        pending?.TrySetResult();

        await CloseCurrentAsync().ConfigureAwait(false);
    }

    private static async Task CloseCurrentAsync()
    {
        ISteamSession? previous;

        lock (_lock)
        {
            previous = _session;
            _session = null;
        }

        if (previous == null)
        {
            return;
        }

        try
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            CSteamLog.Warning(CSteamLog.Steam, $"Error closing the previous session: {ex.Message}");
        }
    }
}
