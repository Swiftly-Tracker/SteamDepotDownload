using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Depot;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed class CDownloadCounter
{
    private readonly IProgress<DownloadProgress>? _progress;
    private readonly Tier0.Shared.Logging.ILoggingTask? _task;
    private readonly uint _depotId;
    private readonly string _label;

    private long _bytesDownloaded;
    private long _bytesTotal;
    private int _filesDownloaded;
    private int _filesSkipped;
    private long _filesRemaining;

    internal CDownloadCounter(uint depotId, string label,
        IProgress<DownloadProgress>? progress, Tier0.Shared.Logging.ILoggingTask? task)
    {
        _depotId = depotId;
        _label = label;
        _progress = progress;
        _task = task;
    }

    internal ulong BytesDownloaded => (ulong)Interlocked.Read(ref _bytesDownloaded);

    internal ulong BytesTotal => (ulong)Interlocked.Read(ref _bytesTotal);

    internal int FilesDownloaded => _filesDownloaded;

    internal int FilesSkipped => _filesSkipped;

    internal void AddTotal(ulong bytes) => Interlocked.Add(ref _bytesTotal, (long)bytes);

    internal void SubtractTotal(ulong bytes) => Interlocked.Add(ref _bytesTotal, -(long)bytes);

    internal void FileDownloaded() => Interlocked.Increment(ref _filesDownloaded);

    internal void FileSkipped() => Interlocked.Increment(ref _filesSkipped);

    internal void SetFilesRemaining(int total) => Interlocked.Exchange(ref _filesRemaining, total);

    internal void FileCompleted(string state, string fileName, ulong size, TimeSpan elapsed)
    {
        var left = Interlocked.Decrement(ref _filesRemaining);

        var timingText = "";
        if (elapsed > TimeSpan.Zero)
        {
            var speed = size / elapsed.TotalSeconds;
            timingText = $", {CDepotFields.FormatBytes((ulong)speed)}/s, {FormatDuration(elapsed)}";
        }

        CSteamLog.Msg(CSteamLog.Depot,
            $"[Depot {_depotId}] {state} {fileName} ({CDepotFields.FormatBytes(size)}{timingText}) ({left} files left)");
    }

    private static string FormatDuration(TimeSpan elapsed)
        => elapsed.TotalSeconds < 1 ? $"{elapsed.TotalMilliseconds:0} ms" : $"{elapsed.TotalSeconds:0.##} s";

    internal void AddDownloaded(ulong bytes, string? currentFile)
    {
        Interlocked.Add(ref _bytesDownloaded, (long)bytes);
        Report(currentFile);
    }

    internal void Report(string? currentFile = null, string? stage = null)
    {
        var downloaded = BytesDownloaded;
        var total = BytesTotal;

        var progress = new DownloadProgress
        {
            DepotId = _depotId,
            BytesDownloaded = downloaded,
            BytesTotal = total,
            CurrentFile = currentFile,
            Stage = stage,
        };

        _progress?.Report(progress);

        if (_task != null)
        {
            _task.Report(progress.Fraction, stage == null ? _label : $"{_label} — {stage}");
        }
    }
}
