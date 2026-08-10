using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CChunkPump
{
    private const int MaxAttemptsPerChunk = 5;
    private const int MaxWriteWorkers = 8;
    private const int RampStartConcurrency = 2;

    private static readonly TimeSpan ChunkStallTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RampInterval = TimeSpan.FromSeconds(1);

    private readonly CSteamSession _session;
    private readonly CCdnServerPool _pool;
    private readonly CResolvedDepot _depot;
    private readonly CDownloadCounter _counter;
    private readonly ConcurrentDictionary<string, ServerStat> _serverStats = new(StringComparer.OrdinalIgnoreCase);

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
        using var _prof = CProfiler.Measure();

        if (queue.IsEmpty)
        {
            return;
        }

        _counter.Report(stage: "downloading");

        var writeWorkerCount = Math.Clamp(Environment.ProcessorCount, 2, MaxWriteWorkers);

        var channel = Channel.CreateBounded<CWriteWork>(new BoundedChannelOptions(
            Math.Max(2, options.MaxDegreeOfParallelism))
        {
            SingleWriter = false,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var writers = Enumerable.Range(0, writeWorkerCount)
            .Select(_ => WriteWorkerAsync(channel.Reader, ct))
            .ToArray();

        var maxConcurrency = Math.Max(1, options.MaxDegreeOfParallelism);
        var rampGate = new SemaphoreSlim(Math.Min(RampStartConcurrency, maxConcurrency), maxConcurrency);
        using var rampCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var rampTask = RunRampAsync(rampGate, maxConcurrency, rampCts.Token);

        Exception? failure = null;

        try
        {
            try
            {
                await Parallel.ForEachAsync(queue, options, async (pending, token) =>
                {
                    await rampGate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        await DownloadChunkAsync(pending, channel.Writer, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        rampGate.Release();
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                channel.Writer.TryComplete();
                rampCts.Cancel();
            }

            await Task.WhenAll(writers).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await rampTask.ConfigureAwait(false);
            rampGate.Dispose();

            foreach (var stream in queue.Select(pending => pending.Stream).Distinct())
            {
                stream.CloseNow();
            }
        }

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        LogServerStats();
    }

    private static async Task RunRampAsync(SemaphoreSlim gate, int maxConcurrency, CancellationToken ct)
    {
        var released = Math.Min(RampStartConcurrency, maxConcurrency);
        if (released >= maxConcurrency)
        {
            return;
        }

        using var timer = new PeriodicTimer(RampInterval);

        try
        {
            while (released < maxConcurrency && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var next = Math.Min(maxConcurrency, released * 2);
                gate.Release(next - released);
                released = next;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WriteWorkerAsync(ChannelReader<CWriteWork> reader, CancellationToken ct)
    {
        await foreach (var work in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            try
            {
                using var writeToDiskProf = CProfiler.Measure("WriteChunkToDiskAsync");

                await work.Pending.Stream
                    .WriteAsync((long)work.Pending.Chunk.Offset, work.Buffer.AsMemory(0, work.Length), ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                _arrayPool.Return(work.Buffer);
            }

            var fileDone = work.Pending.Stream.ChunkFinished();
            if (fileDone)
            {
                _counter.FileCompleted("Downloaded", work.Pending.File.FileName, work.Pending.File.TotalSize,
                    work.Pending.Stream.Elapsed);
            }
        }
    }

    private void LogServerStats()
    {
        foreach (var (host, stat) in _serverStats.OrderByDescending(pair => pair.Value.Bytes))
        {
            var mbps = stat.Bytes * 8.0 / stat.Milliseconds / 1000.0;

            CSteamLog.Detailed(CSteamLog.Cdn,
                $"{host}: {CDepotFields.FormatBytes((ulong)stat.Bytes)} in {stat.Chunks} chunks, " +
                $"{mbps:F0} Mbps avg, {stat.Retries} retries.");
        }
    }

    private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

    private async Task DownloadChunkAsync(CPendingChunk pending, ChannelWriter<CWriteWork> writer,
        CancellationToken ct)
    {
        using var _prof = CProfiler.Measure();

        var chunk = pending.Chunk;
        var buffer = _arrayPool.Rent((int)chunk.UncompressedLength);
        var handedOff = false;

        try
        {
            Exception? last = null;

            for (var attempt = 1; attempt <= MaxAttemptsPerChunk; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var server = _pool.GetConnection();

                try
                {
                    var watch = Stopwatch.StartNew();

                    var downloadTaskProf = CProfiler.Measure("DownloadDepotChunkAsync");

                    var downloadTask = _pool.Client.DownloadDepotChunkAsync(_depot.DepotId, chunk, server,
                        buffer, _depot.DepotKey, _pool.ProxyServer, _cdnToken);

                    downloadTaskProf.Dispose();

                    if (await Task.WhenAny(downloadTask, Task.Delay(ChunkStallTimeout, CancellationToken.None))
                        .ConfigureAwait(false) != downloadTask)
                    {
                        ct.ThrowIfCancellationRequested();

                        throw new TimeoutException(
                            $"Chunk request to {server.Host} stalled past {ChunkStallTimeout}.");
                    }

                    var written = await downloadTask.ConfigureAwait(false);

                    RecordSuccess(server.Host, written, watch.ElapsedMilliseconds);

                    await writer.WriteAsync(new CWriteWork(pending, buffer, written), ct).ConfigureAwait(false);
                    handedOff = true;

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
                                           or IOException or TaskCanceledException or TimeoutException
                                           && !ct.IsCancellationRequested)
                {
                    last = ex;
                    _pool.ReturnBrokenConnection(server);
                    RecordRetry(server.Host);

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
            if (!handedOff)
            {
                _arrayPool.Return(buffer);
            }
        }
    }

    private void RecordSuccess(string? host, int bytes, long milliseconds)
    {
        var stat = _serverStats.GetOrAdd(host ?? "?", static _ => new ServerStat());
        Interlocked.Add(ref stat.Bytes, bytes);
        Interlocked.Add(ref stat.Milliseconds, Math.Max(1, milliseconds));
        Interlocked.Increment(ref stat.Chunks);
    }

    private void RecordRetry(string? host)
    {
        var stat = _serverStats.GetOrAdd(host ?? "?", static _ => new ServerStat());
        Interlocked.Increment(ref stat.Retries);
    }

    private sealed class ServerStat
    {
        internal long Bytes;
        internal long Milliseconds;
        internal int Chunks;
        internal int Retries;
    }

    private readonly record struct CWriteWork(CPendingChunk Pending, byte[] Buffer, int Length);
}
