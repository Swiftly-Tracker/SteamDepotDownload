using SteamDepotDownload.Tier0.Shared.Logging;

namespace SteamDepotDownload.Tier0.Core.Logging;

/// <summary>
/// One unit of work with a label and optional progress, drawn by the terminal while it runs.
/// </summary>
/// <remarks>
/// Every member is guarded, because a task is read and written from different threads by design:
/// whoever is doing the work calls <see cref="Report(double)"/>, while the terminal's redraw timer
/// reads <see cref="Label"/> and <see cref="Progress"/> on its own thread. <see cref="Progress"/> is
/// a <see cref="Nullable{T}"/> of <see cref="double"/> — two fields, not one — so an unguarded read
/// can observe the flag of one write against the value of another and render a fraction that was
/// never reported.
/// </remarks>
internal sealed class CLoggingTask : ILoggingTask
{
    private readonly CLoggingSystem _owner;
    private readonly Lock _gate = new();

    private double? _progress;
    private string _label;
    private bool _running = true;

    public CLoggingTask(CLoggingSystem owner, int channelId, string label, double? progress, LeafCodeInfo source)
    {
        _owner = owner;
        ChannelId = channelId;
        _label = label;
        _progress = progress;
        Source = source;
    }

    public int ChannelId { get; }

    internal LeafCodeInfo Source { get; }

    public string Label
    {
        get
        {
            lock (_gate)
            {
                return _label;
            }
        }
        set
        {
            lock (_gate)
            {
                _label = value;
            }
        }
    }

    public double? Progress
    {
        get
        {
            lock (_gate)
            {
                return _progress;
            }
        }
        set
        {
            lock (_gate)
            {
                _progress = value is double d ? Math.Clamp(d, 0.0, 1.0) : null;
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public void Report(double progress) => Progress = progress;

    /// <summary>Updates both halves of the display at once, so a frame never mixes the two.</summary>
    public void Report(double progress, string label)
    {
        lock (_gate)
        {
            _progress = Math.Clamp(progress, 0.0, 1.0);
            _label = label;
        }
    }

    public void Complete(string? message = null) => Finish(true, message);

    public void Fail(string? message = null) => Finish(false, message);

    public void Dispose() => Finish(true, null);

    /// <summary>
    /// Ends the task exactly once, however many times it is asked to.
    /// </summary>
    /// <remarks>
    /// The <c>using</c> in a caller that also calls <see cref="Complete"/> means two finishes are
    /// the normal case, not an error. Claiming the transition under the lock is what keeps the
    /// second one from logging a duplicate completion message.
    /// </remarks>
    private void Finish(bool ok, string? message)
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
        }

        _owner.EndTask(this, ok, message);
    }
}
