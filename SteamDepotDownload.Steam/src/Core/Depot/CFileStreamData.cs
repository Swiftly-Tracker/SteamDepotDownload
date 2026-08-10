using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CFileStreamData
{
    private readonly object _openLock = new();
    private readonly string _path;
    private readonly Action? _onComplete;
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private SafeFileHandle? _handle;
    private int _remaining;

    internal CFileStreamData(string path, int chunksToDownload, Action? onComplete = null)
    {
        _path = path;
        _remaining = chunksToDownload;
        _onComplete = onComplete;
    }

    internal TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    internal async Task WriteAsync(long offset, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var handle = EnsureHandle();
        await RandomAccess.WriteAsync(handle, data, offset, ct).ConfigureAwait(false);
    }

    private SafeFileHandle EnsureHandle()
    {
        if (_handle is { } existing)
        {
            return existing;
        }

        lock (_openLock)
        {
            return _handle ??= File.OpenHandle(_path, FileMode.Open, FileAccess.Write, FileShare.Read,
                FileOptions.Asynchronous);
        }
    }

    internal bool ChunkFinished()
    {
        if (Interlocked.Decrement(ref _remaining) > 0)
        {
            return false;
        }

        CloseNow();
        _onComplete?.Invoke();
        return true;
    }

    internal void CloseNow()
    {
        lock (_openLock)
        {
            _handle?.Dispose();
            _handle = null;
        }
    }
}

internal readonly record struct CPendingChunk(
    CFileStreamData Stream,
    SteamKit2.DepotManifest.FileData File,
    SteamKit2.DepotManifest.ChunkData Chunk);
