using System.Collections.Concurrent;
using System.Diagnostics;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CDepotFetcher : IDepotFetcher
{
    private readonly CSteamSession _session;
    private readonly CDepotResolver _resolver;

    private static readonly ConcurrentDictionary<string, IDepotStateStore> StateStores =
        new(StringComparer.OrdinalIgnoreCase);

    internal CDepotFetcher(CSteamSession session, DownloadConfig config)
    {
        _session = session;
        Config = config;
        _resolver = new CDepotResolver(session, config);
    }

    public DownloadConfig Config { get; }

    public async Task<DownloadResult> DownloadAppAsync(AppDownloadRequest request,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        using var _prof = CProfiler.Measure();

        await _session.EnsureConnectedAsync(ct).ConfigureAwait(false);

        var depots = await _resolver.ResolveAsync(request, ct).ConfigureAwait(false);

        return await DownloadDepotsAsync(request.AppId, depots, progress, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DepotInfo>> ResolveDepotsAsync(AppDownloadRequest request,
        CancellationToken ct = default)
    {
        await _session.EnsureConnectedAsync(ct).ConfigureAwait(false);
        return await _resolver.DescribeAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<DownloadResult> DownloadPubfileAsync(ulong publishedFileId,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        using var _prof = CProfiler.Measure();

        await _session.EnsureConnectedAsync(ct).ConfigureAwait(false);

        var details = await _session.GetPublishedFileDetailsAsync(publishedFileId, ct).ConfigureAwait(false);

        if (details == null || details.consumer_appid == 0)
        {
            throw new DepotDownloadException(
                $"Published file {publishedFileId} does not exist or is not visible.");
        }

        return await DownloadUgcContentAsync(details.consumer_appid, details.hcontent_file,
            details.file_url, details.filename, ct).ConfigureAwait(false);
    }

    public async Task<DownloadResult> DownloadUgcAsync(uint appId, ulong ugcId,
        IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        using var _prof = CProfiler.Measure();

        await _session.EnsureConnectedAsync(ct).ConfigureAwait(false);

        var details = await _session.GetUgcDetailsAsync(ugcId, ct).ConfigureAwait(false);
        var resolvedAppId = details.AppID != 0 ? details.AppID : appId;

        return await DownloadUgcContentAsync(resolvedAppId, ugcId, details.URL, details.FileName, ct)
            .ConfigureAwait(false);
    }

    private async Task<DownloadResult> DownloadUgcContentAsync(uint appId, ulong contentId,
        string? fileUrl, string? fileName, CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        if (!string.IsNullOrEmpty(fileUrl))
        {
            return await DownloadWebFileAsync(appId, contentId, fileName, fileUrl, ct).ConfigureAwait(false);
        }

        if (contentId is 0 or DepotConstants.InvalidManifestId)
        {
            throw new DepotDownloadException("No downloadable content was found for this item.");
        }

        var key = await _session.GetDepotKeyAsync(appId, appId, ct).ConfigureAwait(false)
            ?? throw new DepotDownloadException($"No decryption key available for app {appId}'s Workshop content.");

        var depot = new CResolvedDepot
        {
            DepotId = appId,
            AppId = appId,
            ManifestId = contentId,
            Branch = DepotConstants.PublicBranch,
            InstallDirectory = _resolver.CreateDirectories(appId, 0),
            DepotKey = key,
        };

        using var pool = new CCdnServerPool(_session, appId);
        await pool.RefreshAsync(Config.CellId == 0 ? null : Config.CellId, ct).ConfigureAwait(false);

        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summary = await DownloadDepotAsync(pool, depot, null, expectedFiles, ct).ConfigureAwait(false);

        return new DownloadResult
        {
            Depots = [summary],
            BytesDownloaded = summary.BytesDownloaded,
            BytesTotal = summary.BytesTotal,
        };
    }

    private async Task<DownloadResult> DownloadWebFileAsync(uint appId, ulong contentId, string? fileName,
        string url, CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var directory = _resolver.CreateDirectories(appId, 0);
        var name = string.IsNullOrEmpty(fileName) ? contentId.ToString() : Path.GetFileName(fileName);
        var path = Path.Combine(directory, name);

        // Workshop items can run several hundred MB; a fixed wall-clock timeout would kill a
        // legitimate slow-connection download. Cancellation is via `ct` (wired to Ctrl+C), not a timer.
        using var http = CHttpFactory.Create(Timeout.InfiniteTimeSpan);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using (var target = File.Create(path))
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        {
            await source.CopyToAsync(target, ct).ConfigureAwait(false);
        }

        var bytes = (ulong)new FileInfo(path).Length;

        return new DownloadResult
        {
            Depots =
            [
                new DepotDownloadSummary
                {
                    DepotId = appId,
                    ManifestId = contentId,
                    InstallDirectory = directory,
                    BytesDownloaded = bytes,
                    BytesTotal = bytes,
                    FilesDownloaded = 1,
                },
            ],
            BytesDownloaded = bytes,
            BytesTotal = bytes,
        };
    }

    public async Task<string> DumpManifestAsync(uint appId, uint depotId, ulong manifestId, string branch,
        CancellationToken ct = default)
    {
        using var _prof = CProfiler.Measure();

        await _session.EnsureConnectedAsync(ct).ConfigureAwait(false);

        var request = new AppDownloadRequest
        {
            AppId = appId,
            Depots = [new DepotSelector(depotId, manifestId)],
            Branch = branch,
        };

        var depots = await _resolver.ResolveAsync(request, ct).ConfigureAwait(false);
        var depot = depots.First();

        using var pool = new CCdnServerPool(_session, depot.AppId);
        await pool.RefreshAsync(Config.CellId == 0 ? null : Config.CellId, ct).ConfigureAwait(false);

        var manifest = await AcquireManifestAsync(pool, depot, ct).ConfigureAwait(false);

        return CManifestDumper.Write(depot.DumpDirectory, manifest);
    }

    private async Task<DownloadResult> DownloadDepotsAsync(uint appId, List<CResolvedDepot> depots,
        IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var stopwatch = Stopwatch.StartNew();
        var summaries = new List<DepotDownloadSummary>(depots.Count);

        using var pool = new CCdnServerPool(_session, appId);
        await pool.RefreshAsync(Config.CellId == 0 ? null : Config.CellId, ct).ConfigureAwait(false);

        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var depot in depots)
        {
            ct.ThrowIfCancellationRequested();

            installDirectories.Add(depot.InstallDirectory);

            var summary = await DownloadDepotAsync(pool, depot, progress, expectedFiles, ct)
                .ConfigureAwait(false);

            summaries.Add(summary);
        }

        if (Config.RemoveUnusedFiles && !Config.ManifestOnly)
        {
            foreach (var directory in installDirectories)
            {
                RemoveUnusedFiles(directory, expectedFiles);
            }
        }

        stopwatch.Stop();

        return new DownloadResult
        {
            Depots = summaries,
            BytesDownloaded = summaries.Aggregate(0UL, (sum, s) => sum + s.BytesDownloaded),
            BytesTotal = summaries.Aggregate(0UL, (sum, s) => sum + s.BytesTotal),
            Elapsed = stopwatch.Elapsed,
        };
    }

    private async Task<DepotDownloadSummary> DownloadDepotAsync(CCdnServerPool pool, CResolvedDepot depot,
        IProgress<DownloadProgress>? progress, HashSet<string> expectedFiles, CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var manifest = await AcquireManifestAsync(pool, depot, ct).ConfigureAwait(false);

        if (Config.ManifestOnly)
        {
            var dumpPath = CManifestDumper.Write(depot.DumpDirectory, manifest);

            CSteamLog.Msg(CSteamLog.Depot, $"Wrote {dumpPath}");

            return new DepotDownloadSummary
            {
                DepotId = depot.DepotId,
                ManifestId = depot.ManifestId,
                InstallDirectory = depot.InstallDirectory,
                ManifestDumpPath = dumpPath,
            };
        }

        var store = GetStateStore(depot);
        var installedManifestId = store.GetInstalledManifest(depot.DepotId);

        var cache = new CManifestCache(_session, depot.ManifestDirectory);

        CManifestData? previous = null;
        if (installedManifestId != DepotConstants.InvalidManifestId)
        {
            previous = cache.TryLoad(depot.DepotId, installedManifestId, out _);
        }

        if (installedManifestId == depot.ManifestId && previous != null && !Config.VerifyAll)
        {
            CSteamLog.Msg(CSteamLog.Depot,
                $"Depot {depot.DepotId} is already at manifest {depot.ManifestId}.");

            foreach (var file in previous.Entries)
            {
                expectedFiles.Add(Path.GetFullPath(Path.Combine(depot.InstallDirectory,
                    file.Name.Replace('\\', Path.DirectorySeparatorChar))));
            }

            return new DepotDownloadSummary
            {
                DepotId = depot.DepotId,
                ManifestId = depot.ManifestId,
                InstallDirectory = depot.InstallDirectory,
                AlreadyInstalled = true,
            };
        }

        var label = $"depot {depot.DepotId}";
        using var task = CSteamLog.BeginProgress(CSteamLog.Depot, label);

        var counter = new CDownloadCounter(depot.DepotId, label, progress, task);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Config.MaxDownloads),
            CancellationToken = ct,
        };

        var stageWatch = Stopwatch.StartNew();

        var vpkPlan = new CVpkExtractionPlanner(depot, Config, manifest).Plan();

        CSteamLog.Detailed(CSteamLog.Depot, $"{label}: vpk plan took {stageWatch.Elapsed}.");
        stageWatch.Restart();

        var effectiveConfig = vpkPlan.ForceIncludePaths.Count > 0
            ? Config with { FileFilter = Config.FileFilter!.WithForcedIncludes(depot.DepotId, vpkPlan.ForceIncludePaths) }
            : Config;

        var vpkGroupTrackers = CVpkExtractionPlanner.BuildGroupTrackers(vpkPlan, depot.InstallDirectory);

        var planner = new CFilePlanner(depot, effectiveConfig, manifest, previous, counter, vpkGroupTrackers);
        var queue = new ConcurrentQueue<CPendingChunk>();

        await planner.PrepareAsync(queue, options, ct).ConfigureAwait(false);

        foreach (var path in planner.ExpectedFiles)
        {
            expectedFiles.Add(path);
        }

        CSteamLog.Detailed(CSteamLog.Depot,
            $"{label}: file prep (verify/allocate {planner.ExpectedFiles.Count()} files) took {stageWatch.Elapsed}.");
        stageWatch.Restart();

        var pump = new CChunkPump(_session, pool, depot, counter);
        await pump.RunAsync(queue, options, ct).ConfigureAwait(false);

        CSteamLog.Detailed(CSteamLog.Depot,
            $"{label}: chunk download ({FormatBytes(counter.BytesDownloaded)}) took {stageWatch.Elapsed}.");
        stageWatch.Restart();

        var listingPaths = await CVpkExtractionPlanner.WriteListings(depot.InstallDirectory, ct)
            .ConfigureAwait(false);

        foreach (var path in listingPaths)
        {
            expectedFiles.Add(path);
        }

        CSteamLog.Detailed(CSteamLog.Depot,
            $"{label}: vpk listing ({listingPaths.Count} files) took {stageWatch.Elapsed}.");
        stageWatch.Restart();

        var extractionTasks = vpkGroupTrackers.Values.Distinct()
            .Select(tracker => tracker.StartExtraction())
            .ToList();

        var extractionResults = await Task.WhenAll(extractionTasks).ConfigureAwait(false);

        foreach (var paths in extractionResults)
        {
            foreach (var path in paths)
            {
                expectedFiles.Add(path);
            }
        }

        CSteamLog.Detailed(CSteamLog.Depot,
            $"{label}: vpk extraction ({extractionTasks.Count} archives) took {stageWatch.Elapsed}.");

        ApplyExecutableFlags(depot, manifest);

        store.Record(new DepotInstallRecord
        {
            DepotId = depot.DepotId,
            ManifestId = depot.ManifestId,
            AppId = depot.AppId,
            Branch = depot.Branch,
            BuildId = depot.BuildId,
            Name = depot.Name,
            SizeOnDisk = manifest.TotalUncompressedSize,
            FileCount = manifest.Entries.Count,
            UpdatedUtc = DateTimeOffset.UtcNow,
        });

        store.Save();

        cache.Prune(depot.DepotId, [depot.ManifestId, installedManifestId]);

        task?.Complete($"{label} done ({FormatBytes(counter.BytesDownloaded)})");

        CSteamLog.Msg(CSteamLog.Depot,
            $"Depot {depot.DepotId}: {counter.FilesDownloaded} files written, " +
            $"{counter.FilesSkipped} already current, {FormatBytes(counter.BytesDownloaded)} downloaded.");

        return new DepotDownloadSummary
        {
            DepotId = depot.DepotId,
            ManifestId = depot.ManifestId,
            InstallDirectory = depot.InstallDirectory,
            BytesDownloaded = counter.BytesDownloaded,
            BytesTotal = counter.BytesTotal,
            FilesDownloaded = counter.FilesDownloaded,
            FilesSkipped = counter.FilesSkipped,
        };
    }

    private async Task<CManifestData> AcquireManifestAsync(CCdnServerPool pool, CResolvedDepot depot,
        CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var cache = new CManifestCache(_session, depot.ManifestDirectory);

        var manifest = cache.TryLoad(depot.DepotId, depot.ManifestId, out var unusable);

        if (unusable)
        {
            CSteamLog.Warning(CSteamLog.Depot,
                $"The cached manifest {depot.ManifestId} for depot {depot.DepotId} could not be read; " +
                "fetching it again.");
        }

        if (manifest != null)
        {
            return manifest;
        }

        var downloaded = await cache
            .DownloadAsync(pool, depot.AppId, depot.DepotId, depot.ManifestId, depot.Branch, depot.DepotKey, ct)
            .ConfigureAwait(false);

        RequireDecryptedFilenames(downloaded, depot);

        manifest = CManifestData.FromSteamKit(downloaded);
        cache.Save(manifest);

        return manifest;
    }

    private static void RequireDecryptedFilenames(DepotManifest manifest, CResolvedDepot depot)
    {
        if (manifest.FilenamesEncrypted && !manifest.DecryptFilenames(depot.DepotKey))
        {
            throw new DepotDownloadException(
                $"The filenames in manifest {depot.ManifestId} for depot {depot.DepotId} could not be " +
                "decrypted with the depot key Steam issued.");
        }
    }

    private IDepotStateStore GetStateStore(CResolvedDepot depot)
    {
        if (Config.StateStore != null)
        {
            return Config.StateStore;
        }

        return StateStores.GetOrAdd(depot.InstallDirectory,
            static directory => new CDepotStateStore(CDepotStateStore.PathFor(directory)));
    }

    private static void ApplyExecutableFlags(CResolvedDepot depot, CManifestData manifest)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var file in manifest.Files)
        {
            if (!file.Flags.HasFlag(SteamKit2.EDepotFileFlag.Executable))
            {
                continue;
            }

            var path = Path.Combine(depot.InstallDirectory,
                file.FileName.Replace('\\', Path.DirectorySeparatorChar));

            if (File.Exists(path))
            {
                CFilePlanner.ApplyFlags(file, path);
            }
        }
    }

    private static void RemoveUnusedFiles(string installDirectory, HashSet<string> expectedFiles)
    {
        using var _prof = CProfiler.Measure();

        var configDirectory = Path.GetFullPath(
            Path.Combine(installDirectory, DepotConstants.ConfigDirectory)) + Path.DirectorySeparatorChar;

        foreach (var path in Directory.EnumerateFiles(installDirectory, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(path);

            if (full.StartsWith(configDirectory, StringComparison.OrdinalIgnoreCase) ||
                expectedFiles.Contains(full))
            {
                continue;
            }

            try
            {
                File.Delete(full);
                CSteamLog.Detailed(CSteamLog.Depot, $"Removed {full}, which no manifest claims.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CSteamLog.Warning(CSteamLog.Depot, $"Could not remove {full}: {ex.Message}");
            }
        }
    }

    private static string FormatBytes(ulong bytes) => CDepotFields.FormatBytes(bytes);
}
