using System.Net;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Tier0.Shared.Serialization;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CManifestCache
{
    private static readonly TimeSpan RequestCodeLifetime = TimeSpan.FromMinutes(5);

    private readonly CSteamSession _session;
    private readonly string _manifestDirectory;

    internal CManifestCache(CSteamSession session, string manifestDirectory)
    {
        _session = session;
        _manifestDirectory = manifestDirectory;
    }

    internal string ManifestPath(uint depotId, ulong manifestId)
        => Path.Combine(_manifestDirectory, $"{depotId}-{manifestId}{DepotConstants.ManifestExtension}");

    internal CManifestData? TryLoad(uint depotId, ulong manifestId, out bool unusable)
    {
        unusable = false;

        var path = ManifestPath(depotId, manifestId);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var file = File.OpenRead(path);

            var manifest = BinaryFormat.Deserialize<CManifestData>(file);

            if (manifest is not { Version: CManifestData.CurrentVersion, FilenamesEncrypted: false })
            {
                unusable = true;
                return null;
            }

            return manifest;
        }
        catch (Exception ex) when (ex is IOException or BinaryFormatException or EndOfStreamException)
        {
            unusable = true;
            return null;
        }
    }

    internal void Save(CManifestData manifest)
    {
        Directory.CreateDirectory(_manifestDirectory);

        var path = ManifestPath(manifest.DepotId, manifest.ManifestGid);
        var temp = $"{path}.{Environment.ProcessId:x}-{Environment.CurrentManagedThreadId:x}.tmp";

        using (var file = File.Create(temp))
        {
            BinaryFormat.Serialize(file, manifest);
        }

        File.Move(temp, path, overwrite: true);
    }

    internal void Prune(uint depotId, IReadOnlyCollection<ulong> keep)
    {
        var wanted = keep
            .Select(manifestId => Path.GetFileName(ManifestPath(depotId, manifestId)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> cached;

        try
        {
            cached = Directory.EnumerateFiles(_manifestDirectory,
                $"{depotId}-*{DepotConstants.ManifestExtension}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var path in cached.ToList())
        {
            if (wanted.Contains(Path.GetFileName(path)))
            {
                continue;
            }

            try
            {
                File.Delete(path);
                CSteamLog.Detailed(CSteamLog.Depot, $"Removed the stale cached manifest {path}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CSteamLog.Warning(CSteamLog.Depot, $"Could not remove {path}: {ex.Message}");
            }
        }
    }

    internal async Task<DepotManifest> DownloadAsync(CCdnServerPool pool, uint appId, uint depotId,
        ulong manifestId, string branch, byte[]? depotKey, CancellationToken ct)
    {
        var requestCode = 0UL;
        var requestCodeExpiry = DateTime.MinValue;

        string? cdnToken = null;
        Exception? last = null;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var server = pool.GetConnection();

            try
            {
                if (requestCode == 0 || DateTime.UtcNow >= requestCodeExpiry)
                {
                    requestCode = await _session
                        .GetManifestRequestCodeAsync(depotId, appId, manifestId, branch, ct)
                        .ConfigureAwait(false);

                    requestCodeExpiry = DateTime.UtcNow + RequestCodeLifetime;

                    if (requestCode == 0)
                    {
                        throw new DepotDownloadException(
                            $"Steam refused a manifest request code for depot {depotId} manifest {manifestId}. " +
                            "The account most likely does not have access to this content.");
                    }
                }

                return await pool.Client.DownloadManifestAsync(depotId, manifestId, requestCode, server,
                    depotKey, pool.ProxyServer, cdnToken).ConfigureAwait(false);
            }
            catch (SteamKitWebRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden && cdnToken == null)
            {
                cdnToken = await _session
                    .GetCdnAuthTokenAsync(appId, depotId, server.Host ?? string.Empty, ct)
                    .ConfigureAwait(false);

                if (cdnToken == null)
                {
                    throw new DepotDownloadException(
                        $"Depot {depotId} needs a CDN auth token and Steam would not issue one.", ex);
                }

                last = ex;
            }
            catch (SteamKitWebRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                throw new DepotDownloadException(
                    $"Manifest {manifestId} for depot {depotId} is unavailable: {ex.StatusCode}.", ex);
            }
            catch (Exception ex) when (ex is SteamKitWebRequestException or HttpRequestException or IOException or TaskCanceledException
                                       && !ct.IsCancellationRequested)
            {
                last = ex;
                CSteamLog.Detailed(CSteamLog.Cdn,
                    $"Manifest fetch from {server.Host} failed ({ex.Message}); trying another server.");
                pool.ReturnBrokenConnection(server);
            }
        }

        throw new DepotDownloadException(
            $"Could not download manifest {manifestId} for depot {depotId}.",
            last ?? new InvalidOperationException("no further detail"));
    }

}
