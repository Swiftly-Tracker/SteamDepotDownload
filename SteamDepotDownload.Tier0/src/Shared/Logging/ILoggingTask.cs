namespace SteamDepotDownload.Tier0.Shared.Logging;

public interface ILoggingTask : IDisposable
{
    string Label { get; set; }
    double? Progress { get; set; }
    bool IsRunning { get; }

    void Report(double progress);
    void Report(double progress, string label);
    void Complete(string? message = null);
    void Fail(string? message = null);
}
