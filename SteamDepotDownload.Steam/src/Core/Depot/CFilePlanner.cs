using System.Collections.Concurrent;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;
using SteamKit2.CDN;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CFilePlanner
{
    private readonly CResolvedDepot _depot;
    private readonly DownloadConfig _config;
    private readonly CManifestData _manifest;
    private readonly Dictionary<string, DepotManifest.FileData> _previous;
    private readonly CDownloadCounter _counter;
    private readonly string _installRoot;
    private readonly IReadOnlyDictionary<string, CVpkGroupTracker>? _vpkGroups;

    private readonly ConcurrentDictionary<string, byte> _expectedFiles =
        new(StringComparer.OrdinalIgnoreCase);

    internal CFilePlanner(CResolvedDepot depot, DownloadConfig config, CManifestData manifest,
        CManifestData? previous, CDownloadCounter counter,
        IReadOnlyDictionary<string, CVpkGroupTracker>? vpkGroups = null)
    {
        _depot = depot;
        _config = config;
        _manifest = manifest;
        _counter = counter;
        _vpkGroups = vpkGroups;
        _installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(depot.InstallDirectory));

        _previous = previous?.Files
            .GroupBy(file => NormalizeKey(file.FileName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, DepotManifest.FileData>(StringComparer.OrdinalIgnoreCase);
    }

    internal IEnumerable<string> ExpectedFiles => _expectedFiles.Keys;

    internal async Task PrepareAsync(ConcurrentQueue<CPendingChunk> queue, ParallelOptions options,
        CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var files = _manifest.Files
            .Where(file => _config.FileFilter?.IsIncluded(_depot.DepotId, file.FileName) ?? true)
            .ToList();

        foreach (var directory in files
            .Where(file => file.Flags.HasFlag(EDepotFileFlag.Directory))
            .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(ResolvePath(directory.FileName));
        }

        var regularFiles = files.Where(file => !file.Flags.HasFlag(EDepotFileFlag.Directory)).ToList();

        foreach (var file in regularFiles)
        {
            _counter.AddTotal(file.TotalSize);
        }

        _counter.SetFilesRemaining(regularFiles.Count);
        _counter.Report(stage: "checking files");

        await Parallel.ForEachAsync(regularFiles, options, async (file, token) =>
        {
            await Task.Yield();
            PrepareFile(file, queue, token);
        }).ConfigureAwait(false);
    }

    private void PrepareFile(DepotManifest.FileData file, ConcurrentQueue<CPendingChunk> queue,
        CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        ct.ThrowIfCancellationRequested();

        var finalPath = ResolvePath(file.FileName);
        _expectedFiles.TryAdd(finalPath, 0);

        var vpkGroup = _vpkGroups?.GetValueOrDefault(NormalizeKey(file.FileName));

        var directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (TryCreateSymlink(file, finalPath))
        {
            _counter.SubtractTotal(file.TotalSize);
            _counter.FileSkipped();
            _counter.FileCompleted("Symlink", file.FileName, file.TotalSize, TimeSpan.Zero);
            vpkGroup?.MarkFileDone();
            return;
        }

        var needed = PlanChunks(file, finalPath);

        if (needed.Count == 0)
        {
            _counter.SubtractTotal(file.TotalSize);
            _counter.FileSkipped();
            _counter.FileCompleted("Validated", file.FileName, file.TotalSize, TimeSpan.Zero);
            ApplyFlags(file, finalPath);
            vpkGroup?.MarkFileDone();
            return;
        }

        var neededBytes = needed.Aggregate(0UL, (sum, chunk) => sum + chunk.UncompressedLength);
        _counter.SubtractTotal(file.TotalSize - Math.Min(file.TotalSize, neededBytes));

        var stream = new CFileStreamData(finalPath, needed.Count, vpkGroup == null ? null : vpkGroup.MarkFileDone);

        foreach (var chunk in needed)
        {
            queue.Enqueue(new CPendingChunk(stream, file, chunk));
        }

        _counter.FileDownloaded();
    }

    private List<DepotManifest.ChunkData> PlanChunks(DepotManifest.FileData file, string finalPath)
    {
        using var _prof = CProfiler.Measure();

        var info = new FileInfo(finalPath);

        if (!info.Exists)
        {
            Allocate(finalPath, file.TotalSize);
            return [.. file.Chunks];
        }

        if (_previous.TryGetValue(NormalizeKey(file.FileName), out var previousFile) && !_config.VerifyAll)
        {
            if (previousFile.FileHash is { Length: > 0 } &&
                file.FileHash is { Length: > 0 } &&
                previousFile.FileHash.SequenceEqual(file.FileHash) &&
                (ulong)info.Length == file.TotalSize)
            {
                return [];
            }

            return Patch(file, previousFile, finalPath);
        }

        return Validate(file, finalPath, info);
    }

    private List<DepotManifest.ChunkData> Patch(DepotManifest.FileData file,
        DepotManifest.FileData previousFile, string finalPath)
    {
        using var _prof = CProfiler.Measure();

        var oldChunks = previousFile.Chunks
            .GroupBy(chunk => chunk.ChunkID!, ChunkIdComparer.Instance)
            .ToDictionary(group => group.Key, group => group.First(), ChunkIdComparer.Instance);

        var matched = new List<(DepotManifest.ChunkData Old, DepotManifest.ChunkData New)>();
        var needed = new List<DepotManifest.ChunkData>();

        foreach (var chunk in file.Chunks)
        {
            if (chunk.ChunkID != null && oldChunks.TryGetValue(chunk.ChunkID, out var old))
            {
                matched.Add((old, chunk));
            }
            else
            {
                needed.Add(chunk);
            }
        }

        if (matched.Count == 0)
        {
            Allocate(finalPath, file.TotalSize);
            return [.. file.Chunks];
        }

        var stagingPath = Path.Combine(_depot.StagingDirectory,
            Path.GetRelativePath(_installRoot, finalPath));

        var stagingDirectory = Path.GetDirectoryName(stagingPath);
        if (!string.IsNullOrEmpty(stagingDirectory))
        {
            Directory.CreateDirectory(stagingDirectory);
        }

        File.Move(finalPath, stagingPath, overwrite: true);

        try
        {
            using var source = new FileStream(stagingPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            destination.SetLength((long)file.TotalSize);

            var buffer = new byte[matched.Max(pair => pair.Old.UncompressedLength)];

            foreach (var (old, current) in matched)
            {
                if ((ulong)source.Length < old.Offset + old.UncompressedLength)
                {
                    needed.Add(current);
                    continue;
                }

                source.Seek((long)old.Offset, SeekOrigin.Begin);
                source.ReadExactly(buffer, 0, (int)old.UncompressedLength);

                if (DepotChunk.AdlerHash(buffer.AsSpan(0, (int)old.UncompressedLength)) != old.Checksum)
                {
                    needed.Add(current);
                    continue;
                }

                destination.Seek((long)current.Offset, SeekOrigin.Begin);
                destination.Write(buffer, 0, (int)old.UncompressedLength);
            }
        }
        finally
        {
            TryDelete(stagingPath);
        }

        return needed;
    }

    private List<DepotManifest.ChunkData> Validate(DepotManifest.FileData file, string finalPath, FileInfo info)
    {
        using var _prof = CProfiler.Measure();

        var needed = new List<DepotManifest.ChunkData>();

        try
        {
            using var stream = new FileStream(finalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            if ((ulong)info.Length != file.TotalSize)
            {
                stream.SetLength((long)file.TotalSize);
            }

            var ordered = file.Chunks.OrderBy(chunk => chunk.Offset).ToList();
            if (ordered.Count == 0)
            {
                return needed;
            }

            var buffer = new byte[ordered.Max(chunk => chunk.UncompressedLength)];

            foreach (var chunk in ordered)
            {
                if ((ulong)stream.Length < chunk.Offset + chunk.UncompressedLength)
                {
                    needed.Add(chunk);
                    continue;
                }

                stream.Seek((long)chunk.Offset, SeekOrigin.Begin);
                stream.ReadExactly(buffer, 0, (int)chunk.UncompressedLength);

                if (DepotChunk.AdlerHash(buffer.AsSpan(0, (int)chunk.UncompressedLength)) != chunk.Checksum)
                {
                    needed.Add(chunk);
                }
            }
        }
        catch (IOException ex)
        {
            CSteamLog.Warning(CSteamLog.Depot,
                $"Could not verify {file.FileName} ({ex.Message}); downloading it again.");

            Allocate(finalPath, file.TotalSize);
            return [.. file.Chunks];
        }

        return needed;
    }

    private static void Allocate(string path, ulong size)
    {
        using var _prof = CProfiler.Measure();

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.SetLength((long)size);
    }

    private bool TryCreateSymlink(DepotManifest.FileData file, string finalPath)
    {
        if (!file.Flags.HasFlag(EDepotFileFlag.Symlink) || string.IsNullOrEmpty(file.LinkTarget))
        {
            return false;
        }

        try
        {
            if (File.Exists(finalPath) || Directory.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.CreateSymbolicLink(finalPath, file.LinkTarget);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            CSteamLog.Warning(CSteamLog.Depot,
                $"Could not create the symlink {file.FileName} -> {file.LinkTarget}: {ex.Message}");
            return true;
        }
    }

    internal static void ApplyFlags(DepotManifest.FileData file, string path)
    {
        if (!file.Flags.HasFlag(EDepotFileFlag.Executable) || OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CSteamLog.Warning(CSteamLog.Depot, $"Could not mark {file.FileName} executable: {ex.Message}");
        }
    }

    private static string NormalizeKey(string manifestPath)
        => manifestPath.Replace('\\', '/').TrimStart('/');

    private string ResolvePath(string manifestPath)
    {
        var relative = manifestPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var full = Path.GetFullPath(Path.Combine(_installRoot, relative));

        if (!full.StartsWith(_installRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !full.Equals(_installRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new DepotDownloadException(
                $"Manifest entry '{manifestPath}' resolves outside the install directory.");
        }

        return full;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private sealed class ChunkIdComparer : IEqualityComparer<byte[]>
    {
        internal static readonly ChunkIdComparer Instance = new();

        public bool Equals(byte[]? x, byte[]? y)
            => ReferenceEquals(x, y) || (x != null && y != null && x.AsSpan().SequenceEqual(y));

        public int GetHashCode(byte[] obj)
        {
            var hash = new HashCode();
            hash.AddBytes(obj);
            return hash.ToHashCode();
        }
    }
}
