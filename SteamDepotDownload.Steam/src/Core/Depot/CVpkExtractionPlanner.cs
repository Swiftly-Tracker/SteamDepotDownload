using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;
using VPKTools.Pak.Shared;
using VPKTools.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed record CVpkExtractionTarget(
    string VpkManifestPath,
    bool ExtractAllEntries,
    IReadOnlySet<string> ExtensionFilter,
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

    private static readonly ConcurrentBag<IPakSystem> PakPool = [];
    private static readonly SemaphoreSlim PakPoolGate = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private static readonly Lock ModuleLoadLock = new();
    private static bool _modulesLoaded;

    private readonly CResolvedDepot _depot;
    private readonly DownloadConfig _config;
    private readonly CManifestData _manifest;

    internal CVpkExtractionPlanner(CResolvedDepot depot, DownloadConfig config, CManifestData manifest)
    {
        _depot = depot;
        _config = config;
        _manifest = manifest;
    }

    internal CVpkExtractionPlan Plan()
    {
        using var _prof = CProfiler.Measure();

        var depotFilter = _config.FileFilter?.ForDepot(_depot.DepotId);
        if (depotFilter == null || !depotFilter.HasVpkRule)
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

        var extensionFilter = new HashSet<string>(depotFilter.VpkExtensions, StringComparer.OrdinalIgnoreCase);

        var forceInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, TargetBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var vpk in vpkFiles)
        {
            if (!depotFilter.IsIncluded(vpk.FileName))
            {
                continue;
            }

            var builder = new TargetBuilder { FromVpkRule = true };
            targets[vpk.FileName] = builder;

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

        var result = targets
            .Select(pair => new CVpkExtractionTarget(pair.Key, pair.Value.ExtractAllEntries,
                pair.Value.ExtensionFilter, pair.Value.FromVpkRule, pair.Value.ArchiveManifestPaths))
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

    internal static IReadOnlyList<string> ExtractTarget(CVpkExtractionTarget target, string installDirectory)
    {
        using var _prof = CProfiler.Measure();

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

        foreach (var entry in pak.GetEntries())
        {
            var extension = Path.GetExtension(entry.Path).TrimStart('.').ToLowerInvariant();
            if (target.ExtensionFilter.Contains(extension))
            {
                written.Add(ExtractOne(pak, entry.Path, destinationDirectory));
            }
        }

        return written;
    }

    private static string ExtractOne(IPakSystem pak, string entryPath, string destinationDirectory)
    {
        var destination = Path.Combine(destinationDirectory, entryPath.Replace('/', Path.DirectorySeparatorChar));
        return pak.ExtractEntry(entryPath, destination);
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
        using var _prof = CProfiler.Measure();

        EnsureModulesLoaded();

        // Extraction runs many VPKs concurrently (one Task.Run per archive group), so each
        // needs its own IPakSystem — the shared singleton the terminal uses can only have one
        // VPK open at a time and would serialize everything behind a single lock.
        PakPoolGate.Wait();

        try
        {
            var pak = PakPool.TryTake(out var pooled) ? pooled : PakSystemFactory.Create();
            pak.Open(vpkPath);

            try
            {
                return action(pak);
            }
            finally
            {
                pak.Close();
                PakPool.Add(pak);
            }
        }
        finally
        {
            PakPoolGate.Release();
        }
    }

    private static void EnsureModulesLoaded()
    {
        if (_modulesLoaded)
        {
            return;
        }

        lock (ModuleLoadLock)
        {
            if (_modulesLoaded)
            {
                return;
            }

            InterfaceSystem.LoadModule("VPKTools.Tier0");
            InterfaceSystem.LoadModule("VPKTools.Pak");
            _modulesLoaded = true;
        }
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
        Interlocked.Decrement(ref _remaining);
    }

    internal Task<IReadOnlyList<string>> StartExtraction()
    {
        var task = Task.Run(() => CVpkExtractionPlanner.ExtractTarget(_target, _installDirectory));
        return Interlocked.CompareExchange(ref _extractionTask, task, null) ?? task;
    }
}
