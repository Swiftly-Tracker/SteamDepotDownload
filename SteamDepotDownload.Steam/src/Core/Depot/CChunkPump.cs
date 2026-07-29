using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CChunkPump
{
    private const int MaxAttemptsPerChunk = 5;

    private readonly CSteamSession _session;
    private readonly CCdnServerPool _pool;
    private readonly CResolvedDepot _depot;
    private readonly CDownloadCounter _counter;

    private string? _cdnToken;

    internal CChunkPump(CSteamSession session, CCdnServerPool pool, CResolvedDepot depot,
        CDownloadCounter counter)
    {
        _session = session;
        _pool = pool;
        _depot = depot;
        _counter = counter;
    }

    internal async Task RunAsync(ConcurrentQueue<CPendingChunk> queue, ParallelOptions options,
        CancellationToken ct)
    {
        if (queue.IsEmpty)
        {
            return;
        }

        _counter.Report(stage: "downloading");

        await Parallel.ForEachAsync(queue, options, async (pending, token) =>
        {
            try
            {
                await DownloadChunkAsync(pending, token).ConfigureAwait(false);
            }
            finally
            {
                pending.Stream.ChunkFinished();
            }
        }).ConfigureAwait(false);
    }

    private async Task DownloadChunkAsync(CPendingChunk pending, CancellationToken ct)
    {
        var chunk = pending.Chunk;
        var buffer = ArrayPool<byte>.Shared.Rent((int)chunk.UncompressedLength);

        try
        {
            Exception? last = null;

            for (var attempt = 1; attempt <= MaxAttemptsPerChunk; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var server = _pool.GetConnection();

                try
                {
                    var written = await _pool.Client.DownloadDepotChunkAsync(_depot.DepotId, chunk, server,
                        buffer, _depot.DepotKey, _pool.ProxyServer, _cdnToken).ConfigureAwait(false);

                    await pending.Stream
                        .WriteAsync((long)chunk.Offset, buffer.AsMemory(0, written), ct)
                        .ConfigureAwait(false);

                    _counter.AddDownloaded(chunk.UncompressedLength, pending.File.FileName);
                    return;
                }
                catch (SteamKitWebRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden && _cdnToken == null)
                {
                    _cdnToken = await _session
                        .GetCdnAuthTokenAsync(_depot.AppId, _depot.DepotId, server.Host ?? string.Empty, ct)
                        .ConfigureAwait(false);

                    last = ex;

                    if (_cdnToken == null)
                    {
                        throw new DepotDownloadException(
                            $"Depot {_depot.DepotId} needs a CDN auth token and Steam would not issue one.", ex);
                    }
                }
                catch (Exception ex) when (ex is SteamKitWebRequestException or HttpRequestException
                                           or IOException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    last = ex;
                    _pool.ReturnBrokenConnection(server);

                    CSteamLog.Detailed(CSteamLog.Cdn,
                        $"Chunk from {server.Host} failed ({ex.Message}); retrying elsewhere.");
                }
            }

            throw new DepotDownloadException(
                $"Gave up on a chunk of {pending.File.FileName} after {MaxAttemptsPerChunk} attempts.",
                last ?? new InvalidOperationException("no further detail"));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
