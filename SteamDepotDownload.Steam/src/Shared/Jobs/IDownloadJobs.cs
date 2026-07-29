using SteamDepotDownload.Steam.Shared.Depot;

namespace SteamDepotDownload.Steam.Shared.Jobs;

public enum DownloadJobState
{
    Running,
    Completed,
    Failed,
    Cancelled,
}

public sealed record DownloadJobStatus
{
    public required int Id { get; init; }

    public required string Label { get; init; }

    public required DownloadJobState State { get; init; }

    public double Fraction { get; init; }

    public string? Detail { get; init; }

    public string? Error { get; init; }
}

public interface IDownloadJobs
{
    int Start(string label, Func<IProgress<DownloadProgress>, CancellationToken, Task> work);

    bool Cancel(int id);

    void CancelAll();

    IReadOnlyList<DownloadJobStatus> GetJobs();

    Task WaitAsync(int id, CancellationToken ct = default);
}
