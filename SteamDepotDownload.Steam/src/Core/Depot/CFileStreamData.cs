namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CFileStreamData
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _path;
    private readonly Action? _onComplete;
    private FileStream? _stream;
    private int _remaining;

    internal CFileStreamData(string path, int chunksToDownload, Action? onComplete = null)
    {
        _path = path;
        _remaining = chunksToDownload;
        _onComplete = onComplete;
    }

    internal async Task WriteAsync(long offset, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            _stream ??= new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read);
            _stream.Seek(offset, SeekOrigin.Begin);
            await _stream.WriteAsync(data, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    internal void ChunkFinished()
    {
        if (Interlocked.Decrement(ref _remaining) > 0)
        {
            return;
        }

        _lock.Wait();

        try
        {
            _stream?.Dispose();
            _stream = null;
        }
        finally
        {
            _lock.Release();
        }

        _onComplete?.Invoke();
    }

    internal void CloseNow()
    {
        _lock.Wait();

        try
        {
            _stream?.Dispose();
            _stream = null;
        }
        finally
        {
            _lock.Release();
        }
    }
}

internal readonly record struct CPendingChunk(
    CFileStreamData Stream,
    SteamKit2.DepotManifest.FileData File,
    SteamKit2.DepotManifest.ChunkData Chunk);
