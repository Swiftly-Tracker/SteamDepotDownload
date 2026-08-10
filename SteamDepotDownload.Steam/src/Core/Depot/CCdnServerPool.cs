using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Auth;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2.CDN;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CCdnServerPool : IDisposable
{
    private readonly CSteamSession _session;
    private readonly IAccountSettingsStore _store;
    private readonly uint _appId;
    private readonly Lock _lock = new();

    private List<Server> _servers = [];
    private int _next;

    internal CCdnServerPool(CSteamSession session, uint appId)
    {
        _session = session;
        _store = session.Store;
        _appId = appId;
        Client = session.CreateCdnClient();
    }

    internal Client Client { get; }

    internal Server? ProxyServer { get; private set; }

    internal int ServerCount
    {
        get
        {
            lock (_lock)
            {
                return _servers.Count;
            }
        }
    }

    internal async Task RefreshAsync(uint? cellId, CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var servers = await _session.GetContentServersAsync(cellId, ct).ConfigureAwait(false);

        var proxy = servers.FirstOrDefault(server => server.UseAsProxy);

        var usable = servers
            .Where(server => server.Type is "SteamCache" or "CDN")
            .Where(server => server.AllowedAppIds.Length == 0 || server.AllowedAppIds.Contains(_appId))
            .OrderBy(server => _store.GetContentServerPenalty(server.Host ?? string.Empty))
            .ThenBy(server => server.WeightedLoad)
            .ToList();

        var ring = new List<Server>(usable.Count);

        foreach (var server in usable)
        {
            for (var i = 0; i < Math.Max(1, server.NumEntries); i++)
            {
                ring.Add(server);
            }
        }

        lock (_lock)
        {
            _servers = ring;
            _next = 0;
            ProxyServer = proxy;
        }

        CSteamLog.Detailed(CSteamLog.Cdn,
            $"{_servers.Count} content servers available for app {_appId}.");

        if (ring.Count == 0)
        {
            throw new DepotDownloadException($"Steam returned no usable content servers for app {_appId}.");
        }
    }

    internal Server GetConnection()
    {
        using var _prof = CProfiler.Measure();

        lock (_lock)
        {
            if (_servers.Count == 0)
            {
                throw new DepotDownloadException("The content server pool is empty.");
            }

            var server = _servers[(int)((uint)_next % (uint)_servers.Count)];
            _next++;
            return server;
        }
    }

    internal void ReturnBrokenConnection(Server? server)
    {
        if (server?.Host is { } host)
        {
            _store.SetContentServerPenalty(host, _store.GetContentServerPenalty(host) + 1);
        }
    }

    public void Dispose()
    {
        Client.Dispose();
        _store.Save();
    }
}
