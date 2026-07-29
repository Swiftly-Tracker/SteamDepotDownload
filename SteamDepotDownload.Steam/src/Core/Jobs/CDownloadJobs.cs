using System.Collections.Concurrent;
using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Interfaces;
using SteamDepotDownload.Steam.Shared.Jobs;
using SteamDepotDownload.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Steam.Core.Jobs;

[ExposeInterface(SteamInterfaceNames.DownloadJobs)]
internal sealed class CDownloadJobs : IDownloadJobs
{
    private readonly ConcurrentDictionary<int, CJob> _jobs = new();
    private int _nextId;

    public int Start(string label, Func<IProgress<DownloadProgress>, CancellationToken, Task> work)
    {
        var id = Interlocked.Increment(ref _nextId);
        var job = new CJob(id, label);

        _jobs[id] = job;

        job.Task = Task.Run(async () =>
        {
            try
            {
                await work(job, job.Cancellation.Token).ConfigureAwait(false);
                job.Finish(DownloadJobState.Completed, null);
            }
            catch (OperationCanceledException)
            {
                job.Finish(DownloadJobState.Cancelled, null);
            }
            catch (Exception ex)
            {
                job.Finish(DownloadJobState.Failed, ex.Message);
            }
        });

        return id;
    }

    public bool Cancel(int id)
    {
        if (!_jobs.TryGetValue(id, out var job) || job.State != DownloadJobState.Running)
        {
            return false;
        }

        job.Cancellation.Cancel();
        return true;
    }

    public void CancelAll()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.State == DownloadJobState.Running)
            {
                job.Cancellation.Cancel();
            }
        }
    }

    public IReadOnlyList<DownloadJobStatus> GetJobs()
        => [.. _jobs.Values.OrderBy(job => job.Id).Select(job => job.Snapshot())];

    public async Task WaitAsync(int id, CancellationToken ct = default)
    {
        if (!_jobs.TryGetValue(id, out var job) || job.Task == null)
        {
            return;
        }

        await job.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    private sealed class CJob : IProgress<DownloadProgress>
    {
        private readonly Lock _lock = new();
        private double _fraction;
        private string? _detail;
        private string? _error;
        private DownloadJobState _state = DownloadJobState.Running;

        internal CJob(int id, string label)
        {
            Id = id;
            Label = label;
        }

        internal int Id { get; }

        internal string Label { get; }

        internal CancellationTokenSource Cancellation { get; } = new();

        internal Task? Task { get; set; }

        internal DownloadJobState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
        }

        public void Report(DownloadProgress value)
        {
            lock (_lock)
            {
                _fraction = value.Fraction;
                _detail = value.CurrentFile ?? value.Stage;
            }
        }

        internal void Finish(DownloadJobState state, string? error)
        {
            lock (_lock)
            {
                _state = state;
                _error = error;

                if (state == DownloadJobState.Completed)
                {
                    _fraction = 1d;
                }
            }

            switch (state)
            {
                case DownloadJobState.Completed:
                    CSteamLog.Msg(CSteamLog.Depot, $"[{Id}] {Label} finished.");
                    break;

                case DownloadJobState.Cancelled:
                    CSteamLog.Warning(CSteamLog.Depot, $"[{Id}] {Label} cancelled.");
                    break;

                case DownloadJobState.Failed:
                    CSteamLog.Warning(CSteamLog.Depot, $"[{Id}] {Label} failed: {error}");
                    break;
            }
        }

        internal DownloadJobStatus Snapshot()
        {
            lock (_lock)
            {
                return new DownloadJobStatus
                {
                    Id = Id,
                    Label = Label,
                    State = _state,
                    Fraction = _fraction,
                    Detail = _detail,
                    Error = _error,
                };
            }
        }
    }
}
