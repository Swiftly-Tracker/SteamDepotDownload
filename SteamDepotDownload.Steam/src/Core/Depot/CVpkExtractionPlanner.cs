using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;
using VPKTools.Pak.Shared;
using VPKTools.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed record CVpkExtractionTarget(
    string VpkManifestPath,
    bool ExtractAllEntries,
    IReadOnlySet<string> ExtensionFilter,
    IReadOnlyList<string> SpecificEntries,
    bool FromVpkRule,
    IReadOnlyList<string> ArchiveManifestPaths);

internal sealed record CVpkExtractionPlan(IReadOnlyList<CVpkExtractionTarget> Targets, IReadOnlySet<string> ForceIncludePaths)
{
    internal static readonly CVpkExtractionPlan Empty = new([], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

internal sealed class CVpkExtractionPlanner
{
    private static readonly Regex VpkNameRegex = new(@"\.vpk$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DirVpkNameRegex = new(@"_dir\.vpk$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const string DirVpkSuffix = "_dir.vpk";

    private static readonly object PakLock = new();
    private static IPakSystem? _pak;

    private readonly CSteamSession _session;
    private readonly CCdnServerPool _pool;
    private readonly CResolvedDepot _depot;
    private readonly DownloadConfig _config;
    private readonly CManifestData _manifest;
    private readonly CManifestData? _previous;

    internal CVpkExtractionPlanner(CSteamSession session, CCdnServerPool pool, CResolvedDepot depot,
        DownloadConfig config, CManifestData manifest, CManifestData? previous)
    {
        _session = session;
        _pool = pool;
        _depot = depot;
        _config = config;
        _manifest = manifest;
        _previous = previous;
    }

    internal async Task<CVpkExtractionPlan> PlanAsync(ParallelOptions options, CancellationToken ct)
    {
        var depotFilter = _config.FileFilter?.ForDepot(_depot.DepotId);
        if (depotFilter == null)
        {
            return CVpkExtractionPlan.Empty;
        }

        var vpkFiles = _manifest.Files
            .Where(file => !file.Flags.HasFlag(EDepotFileFlag.Directory) && VpkNameRegex.IsMatch(file.FileName))
            .ToList();

        if (vpkFiles.Count == 0)
        {
            return CVpkExtractionPlan.Empty;
        }

        var wantsExtraction = depotFilter.HasVpkRule;
        var extensionFilter = new HashSet<string>(depotFilter.VpkExtensions, StringComparer.OrdinalIgnoreCase);

        var dirVpks = vpkFiles.Where(file => DirVpkNameRegex.IsMatch(file.FileName)).ToList();

        var orphanLiterals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<Regex> orphanPatterns = [];

        if (dirVpks.Count > 0 && (depotFilter.Literals.Count > 0 || depotFilter.Patterns.Count > 0))
        {
            var allNames = new HashSet<string>(
                _manifest.Files
                    .Where(file => !file.Flags.HasFlag(EDepotFileFlag.Directory))
                    .Select(file => Normalize(file.FileName)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var literal in depotFilter.Literals)
            {
                if (!allNames.Contains(literal))
                {
                    orphanLiterals.Add(literal);
                }
            }

            foreach (var pattern in depotFilter.Patterns)
            {
                if (!allNames.Any(pattern.IsMatch))
                {
                    orphanPatterns.Add(pattern);
                }
            }
        }

        if (!wantsExtraction && orphanLiterals.Count == 0 && orphanPatterns.Count == 0)
        {
            return CVpkExtractionPlan.Empty;
        }

        var forceInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, TargetBuilder>(StringComparer.OrdinalIgnoreCase);

        TargetBuilder GetOrAddTarget(string path)
        {
            if (!targets.TryGetValue(path, out var builder))
            {
                builder = new TargetBuilder();
                targets[path] = builder;
            }

            return builder;
        }

        if (orphanLiterals.Count > 0 || orphanPatterns.Count > 0)
        {
            await DownloadFilesAsync(dirVpks.Select(file => file.FileName), options, ct).ConfigureAwait(false);

            foreach (var dirVpk in dirVpks)
            {
                ct.ThrowIfCancellationRequested();

                var resolvedPath = ResolveInstallPath(_depot.InstallDirectory, dirVpk.FileName);

                IReadOnlyList<PakEntryInfo> entries;
                try
                {
                    entries = WithPak(resolvedPath, pak => pak.GetEntries());
                }
                catch (Exception ex)
                {
                    CSteamLog.Warning(CSteamLog.Depot, $"Could not inspect {dirVpk.FileName}: {ex.Message}");
                    continue;
                }

                var matched = entries
                    .Where(entry =>
                    {
                        var normalized = Normalize(entry.Path);
                        return orphanLiterals.Contains(normalized) || orphanPatterns.Any(pattern => pattern.IsMatch(normalized));
                    })
                    .Select(entry => entry.Path)
                    .ToList();

                if (matched.Count == 0)
                {
                    continue;
                }

                var archiveGroup = BuildArchiveGroup(dirVpk.FileName);
                var builder = GetOrAddTarget(dirVpk.FileName);
                builder.SpecificEntries.AddRange(matched);
                builder.ArchiveManifestPaths = archiveGroup;

                foreach (var path in archiveGroup)
                {
                    forceInclude.Add(path);
                }
            }
        }

        if (wantsExtraction)
        {
            foreach (var vpk in vpkFiles)
            {
                if (!depotFilter.IsIncluded(vpk.FileName))
                {
                    continue;
                }

                var builder = GetOrAddTarget(vpk.FileName);
                builder.FromVpkRule = true;

                if (extensionFilter.Count == 0)
                {
                    builder.ExtractAllEntries = true;
                }
                else
                {
                    builder.ExtensionFilter.UnionWith(extensionFilter);
                }

                var archiveGroup = BuildArchiveGroup(vpk.FileName);
                builder.ArchiveManifestPaths = archiveGroup;

                foreach (var path in archiveGroup)
                {
                    forceInclude.Add(path);
                }
            }
        }

        var result = targets
            .Select(pair => new CVpkExtractionTarget(pair.Key, pair.Value.ExtractAllEntries,
                pair.Value.ExtensionFilter, pair.Value.SpecificEntries, pair.Value.FromVpkRule,
                pair.Value.ArchiveManifestPaths))
            .ToList();

        return result.Count == 0 ? CVpkExtractionPlan.Empty : new CVpkExtractionPlan(result, forceInclude);
    }

    private List<string> BuildArchiveGroup(string vpkManifestPath)
    {
        var group = new List<string> { vpkManifestPath };

        if (DirVpkNameRegex.IsMatch(vpkManifestPath))
        {
            group.AddRange(FindCompanions(vpkManifestPath));
        }

        return group;
    }

    internal static Dictionary<string, CVpkGroupTracker> BuildGroupTrackers(CVpkExtractionPlan plan, string installDirectory)
    {
        var trackers = new Dictionary<string, CVpkGroupTracker>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in plan.Targets)
        {
            var tracker = new CVpkGroupTracker(target, installDirectory);

            foreach (var path in target.ArchiveManifestPaths)
            {
                trackers[Normalize(path)] = tracker;
            }
        }

        return trackers;
    }

    private IEnumerable<string> FindCompanions(string dirVpkManifestPath)
    {
        var normalized = Normalize(dirVpkManifestPath);
        var slash = normalized.LastIndexOf('/');
        var directory = slash >= 0 ? normalized[..(slash + 1)] : string.Empty;
        var fileName = slash >= 0 ? normalized[(slash + 1)..] : normalized;
        var group = fileName[..^DirVpkSuffix.Length];

        var companionRegex = new Regex(
            $"^{Regex.Escape(directory)}{Regex.Escape(group)}_\\d+\\.vpk$",
            RegexOptions.IgnoreCase);

        return _manifest.Files
            .Where(file => !file.Flags.HasFlag(EDepotFileFlag.Directory) && companionRegex.IsMatch(Normalize(file.FileName)))
            .Select(file => file.FileName);
    }

    private async Task DownloadFilesAsync(IEnumerable<string> manifestPaths, ParallelOptions options, CancellationToken ct)
    {
        var miniFilter = FileFilter.FromLines(manifestPaths);
        if (miniFilter.IsEmpty)
        {
            return;
        }

        var miniConfig = _config with { FileFilter = miniFilter };
        var miniCounter = new CDownloadCounter(_depot.DepotId, $"depot {_depot.DepotId} vpk metadata",
            progress: null, task: null);

        var miniPlanner = new CFilePlanner(_depot, miniConfig, _manifest, _previous, miniCounter);
        var queue = new ConcurrentQueue<CPendingChunk>();

        await miniPlanner.PrepareAsync(queue, options, ct).ConfigureAwait(false);

        var miniPump = new CChunkPump(_session, _pool, _depot, miniCounter);
        await miniPump.RunAsync(queue, options, ct).ConfigureAwait(false);
    }

    internal static IReadOnlyList<string> ExtractTarget(CVpkExtractionTarget target, string installDirectory)
    {
        var resolvedPath = ResolveInstallPath(installDirectory, target.VpkManifestPath);

        if (!File.Exists(resolvedPath))
        {
            CSteamLog.Warning(CSteamLog.Depot,
                $"Expected VPK {target.VpkManifestPath} was not downloaded; skipping extraction.");
            return [];
        }

        var destinationDirectory = target.FromVpkRule
            ? Path.Combine(Path.GetDirectoryName(resolvedPath)!, GetGroupFolderName(target.VpkManifestPath))
            : Path.GetDirectoryName(resolvedPath)!;

        List<string> extractedPaths;

        try
        {
            extractedPaths = WithPak(resolvedPath, pak => ExtractEntries(pak, target, destinationDirectory));
        }
        catch (Exception ex)
        {
            CSteamLog.Warning(CSteamLog.Depot, $"Failed to extract {target.VpkManifestPath}: {ex.Message}");
            return [];
        }

        if (!target.FromVpkRule)
        {
            return extractedPaths;
        }

        DeleteArchiveGroup(installDirectory, target.ArchiveManifestPaths);

        return extractedPaths;
    }

    private static void DeleteArchiveGroup(string installDirectory, IReadOnlyList<string> archiveManifestPaths)
    {
        foreach (var manifestPath in archiveManifestPaths)
        {
            var resolvedPath = ResolveInstallPath(installDirectory, manifestPath);

            try
            {
                File.Delete(resolvedPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CSteamLog.Warning(CSteamLog.Depot, $"Could not remove {manifestPath}: {ex.Message}");
            }
        }
    }

    private static List<string> ExtractEntries(IPakSystem pak, CVpkExtractionTarget target, string destinationDirectory)
    {
        var written = new List<string>();

        if (target.ExtractAllEntries)
        {
            foreach (var entry in pak.GetEntries())
            {
                written.Add(ExtractOne(pak, entry.Path, destinationDirectory));
            }

            return written;
        }

        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (target.ExtensionFilter.Count > 0)
        {
            foreach (var entry in pak.GetEntries())
            {
                var extension = Path.GetExtension(entry.Path).TrimStart('.').ToLowerInvariant();
                if (target.ExtensionFilter.Contains(extension) && done.Add(entry.Path))
                {
                    written.Add(ExtractOne(pak, entry.Path, destinationDirectory));
                }
            }
        }

        foreach (var entryPath in target.SpecificEntries)
        {
            if (!done.Add(entryPath))
            {
                continue;
            }

            written.Add(ExtractOne(pak, entryPath, destinationDirectory));
        }

        return written;
    }

    private static string ExtractOne(IPakSystem pak, string entryPath, string destinationDirectory)
    {
        var destination = Path.Combine(destinationDirectory, entryPath.Replace('/', Path.DirectorySeparatorChar));
        pak.ExtractEntry(entryPath, destination);
        return destination;
    }

    private static string GetGroupFolderName(string manifestPath)
    {
        var fileName = Path.GetFileName(Normalize(manifestPath));

        return DirVpkNameRegex.IsMatch(fileName)
            ? fileName[..^DirVpkSuffix.Length]
            : fileName[..^".vpk".Length];
    }

    private static T WithPak<T>(string vpkPath, Func<IPakSystem, T> action)
    {
        lock (PakLock)
        {
            var pak = EnsurePakSystem();
            pak.Open(vpkPath);

            try
            {
                return action(pak);
            }
            finally
            {
                pak.Close();
            }
        }
    }

    private static IPakSystem EnsurePakSystem()
    {
        if (_pak != null)
        {
            return _pak;
        }

        InterfaceSystem.LoadModule("VPKTools.Tier0");
        InterfaceSystem.LoadModule("VPKTools.Pak");

        _pak = InterfaceSystem.GetInterface<IPakSystem>(PakInterfaceNames.Pak)
            ?? throw new DepotDownloadException("The VPKTools pak system interface could not be resolved.");

        return _pak;
    }

    private static string ResolveInstallPath(string installDirectory, string manifestPath)
    {
        var relative = manifestPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(installDirectory, relative));
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private sealed class TargetBuilder
    {
        internal bool ExtractAllEntries;
        internal bool FromVpkRule;
        internal HashSet<string> ExtensionFilter { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal List<string> SpecificEntries { get; } = [];
        internal IReadOnlyList<string> ArchiveManifestPaths { get; set; } = [];
    }
}

internal sealed class CVpkGroupTracker
{
    private readonly CVpkExtractionTarget _target;
    private readonly string _installDirectory;
    private int _remaining;
    private Task<IReadOnlyList<string>>? _extractionTask;

    internal CVpkGroupTracker(CVpkExtractionTarget target, string installDirectory)
    {
        _target = target;
        _installDirectory = installDirectory;
        _remaining = Math.Max(1, target.ArchiveManifestPaths.Count);
    }

    internal Task<IReadOnlyList<string>>? ExtractionTask => Volatile.Read(ref _extractionTask);

    internal void MarkFileDone()
    {
        if (Interlocked.Decrement(ref _remaining) > 0)
        {
            return;
        }

        var task = Task.Run(() => CVpkExtractionPlanner.ExtractTarget(_target, _installDirectory));
        Interlocked.CompareExchange(ref _extractionTask, task, null);
    }
}
