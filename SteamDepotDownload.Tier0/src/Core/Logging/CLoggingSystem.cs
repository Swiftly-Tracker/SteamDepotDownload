using System.Diagnostics;
using SteamDepotDownload.Tier0.Shared.Drawing;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Logging;

namespace SteamDepotDownload.Tier0.Core.Logging;

[ExposeInterface(InterfaceNames.LoggingSystem)]
internal sealed class CLoggingSystem : ILoggingSystem
{
    private sealed class Channel
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public LoggingChannelFlags Flags { get; set; }
        public LoggingVerbosity Verbosity { get; set; }
        public Color Color { get; set; }
        public List<string> Tags { get; } = [];
    }

    private readonly Lock _lock = new();
    private readonly List<Channel> _channels = [];
    private readonly List<ILoggingListener> _listeners = [];
    private readonly List<CLoggingTask> _tasks = [];
    private ILoggingResponsePolicy _policy = new CDefaultLoggingResponsePolicy();

    public int RegisterChannel(string name, LoggingChannelFlags flags = LoggingChannelFlags.None,
        LoggingVerbosity verbosity = LoggingVerbosity.Default, Color color = default)
    {
        name = Truncate(name);

        lock (_lock)
        {
            var existing = FindChannelLocked(name);
            if (existing >= 0)
            {
                return existing;
            }

            if (_channels.Count >= ILoggingSystem.MaxChannelCount)
            {
                throw new InvalidOperationException(
                    $"Cannot register logging channel '{name}': maximum of {ILoggingSystem.MaxChannelCount} channels reached.");
            }

            var channel = new Channel
            {
                Id = _channels.Count,
                Name = name,
                Flags = flags,
                Verbosity = verbosity,
                Color = color,
            };
            _channels.Add(channel);

            foreach (var listener in _listeners)
            {
                listener.OnChannelRegistered(channel.Id);
            }

            return channel.Id;
        }
    }

    public int FindChannel(string name)
    {
        lock (_lock)
        {
            return FindChannelLocked(name);
        }
    }

    public int ChannelCount
    {
        get
        {
            lock (_lock)
            {
                return _channels.Count;
            }
        }
    }

    public void AddTagToChannel(int channelId, string tag)
    {
        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            if (channel != null && !channel.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                channel.Tags.Add(Truncate(tag));
            }
        }
    }

    public bool HasTag(int channelId, string tag)
    {
        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            return channel != null && channel.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool IsChannelEnabled(int channelId, LoggingVerbosity verbosity)
    {
        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            return channel != null && verbosity <= channel.Verbosity;
        }
    }

    public bool IsChannelEnabled(int channelId, LoggingSeverity severity)
        => IsChannelEnabled(channelId, RequiredVerbosity(severity));

    public string? GetChannelName(int channelId)
    {
        lock (_lock)
        {
            return GetChannelLocked(channelId)?.Name;
        }
    }

    public LoggingVerbosity GetChannelVerbosity(int channelId)
    {
        lock (_lock)
        {
            return GetChannelLocked(channelId)?.Verbosity ?? LoggingVerbosity.Off;
        }
    }

    public void SetChannelVerbosity(int channelId, LoggingVerbosity verbosity)
    {
        ILoggingListener[] listeners;
        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            if (channel == null)
            {
                return;
            }

            channel.Verbosity = verbosity;
            listeners = [.. _listeners];
        }

        foreach (var listener in listeners)
        {
            listener.OnChannelVerbosityChanged(channelId);
        }
    }

    public void SetChannelVerbosityByName(string name, LoggingVerbosity verbosity)
    {
        int id = FindChannel(name);
        if (id >= 0)
        {
            SetChannelVerbosity(id, verbosity);
        }
    }

    public void SetChannelVerbosityByTag(string tag, LoggingVerbosity verbosity)
    {
        int[] ids;
        lock (_lock)
        {
            ids = [.. _channels
                .Where(c => c.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .Select(c => c.Id)];
        }

        foreach (var id in ids)
        {
            SetChannelVerbosity(id, verbosity);
        }
    }

    public Color GetChannelColor(int channelId)
    {
        lock (_lock)
        {
            return GetChannelLocked(channelId)?.Color ?? default;
        }
    }

    public void SetChannelColor(int channelId, Color color)
    {
        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            if (channel != null)
            {
                channel.Color = color;
            }
        }
    }

    public LoggingChannelFlags GetChannelFlags(int channelId)
    {
        lock (_lock)
        {
            return GetChannelLocked(channelId)?.Flags ?? LoggingChannelFlags.None;
        }
    }

    public void SetChannelFlags(int channelId, LoggingChannelFlags flags)
    {
        ILoggingListener[] listeners;
        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            if (channel == null)
            {
                return;
            }

            channel.Flags = flags;
            listeners = [.. _listeners];
        }

        foreach (var listener in listeners)
        {
            listener.OnChannelFlagsChanged(channelId);
        }
    }

    public void RegisterListener(ILoggingListener listener)
    {
        lock (_lock)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }
    }

    public void UnregisterListener(ILoggingListener listener)
    {
        lock (_lock)
        {
            _listeners.Remove(listener);
        }
    }

    public bool IsListenerRegistered(ILoggingListener listener)
    {
        lock (_lock)
        {
            return _listeners.Contains(listener);
        }
    }

    public void SetResponsePolicy(ILoggingResponsePolicy? policy)
    {
        lock (_lock)
        {
            _policy = policy ?? new CDefaultLoggingResponsePolicy();
        }
    }

    public ILoggingTask BeginSpinner(int channelId, string label,
        string? file = null, int line = 0, string? function = null)
        => AddTask(channelId, label, null, new LeafCodeInfo(file, line, function));

    public ILoggingTask BeginProgress(int channelId, string label,
        string? file = null, int line = 0, string? function = null)
        => AddTask(channelId, label, 0.0, new LeafCodeInfo(file, line, function));

    public IReadOnlyList<ILoggingTask> ActiveTasks
    {
        get
        {
            lock (_lock)
            {
                return [.. _tasks];
            }
        }
    }

    private ILoggingTask AddTask(int channelId, string label, double? progress, LeafCodeInfo source)
    {
        var task = new CLoggingTask(this, channelId, label, progress, source);
        lock (_lock)
        {
            _tasks.Add(task);
        }
        return task;
    }

    internal void EndTask(CLoggingTask task, bool ok, string? message)
    {
        lock (_lock)
        {
            _tasks.Remove(task);
        }

        if (message != null)
        {
            string glyph = ok ? "✓" : "✗";
            Log(task.ChannelId, ok ? LoggingSeverity.Message : LoggingSeverity.Warning, $"{glyph} {message}",
                task.Source.File, task.Source.Line, task.Source.Function);
        }
    }

    public LoggingResponse Log(int channelId, LoggingSeverity severity, string message,
        string? file = null, int line = 0, string? function = null)
        => Log(channelId, severity, default, message, file, line, function);

    public LoggingResponse Log(int channelId, LoggingSeverity severity, Color color, string message,
        string? file = null, int line = 0, string? function = null)
    {
        LoggingContext context;
        ILoggingListener[] listeners;
        ILoggingResponsePolicy policy;

        lock (_lock)
        {
            var channel = GetChannelLocked(channelId);
            if (channel == null || RequiredVerbosity(severity) > channel.Verbosity)
            {
                return LoggingResponse.Continue;
            }

            var effectiveColor = color.A == 0 ? channel.Color : color;

            context = new LoggingContext
            {
                ChannelId = channel.Id,
                ChannelName = channel.Name,
                Flags = channel.Flags,
                Severity = severity,
                Verbosity = channel.Verbosity,
                Color = effectiveColor,
                Source = new LeafCodeInfo(file, line, function),
            };

            listeners = [.. _listeners];
            policy = _policy;
        }

        foreach (var listener in listeners)
        {
            listener.Log(context, message);
        }

        var response = policy.OnLog(context);
        switch (response)
        {
            case LoggingResponse.Debugger:
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                break;

            case LoggingResponse.Abort:
                foreach (var listener in listeners)
                {
                    listener.OnFlush();
                }
                Environment.Exit(1);
                break;
        }

        return response;
    }

    private static LoggingVerbosity RequiredVerbosity(LoggingSeverity severity) => severity switch
    {
        LoggingSeverity.Detailed => LoggingVerbosity.Detailed,
        LoggingSeverity.Message => LoggingVerbosity.Default,
        _ => LoggingVerbosity.Essential,
    };

    private static string Truncate(string name)
        => name.Length > ILoggingSystem.MaxIdentifierLength
            ? name[..ILoggingSystem.MaxIdentifierLength]
            : name;

    private int FindChannelLocked(string name)
    {
        foreach (var channel in _channels)
        {
            if (string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return channel.Id;
            }
        }
        return -1;
    }

    private Channel? GetChannelLocked(int channelId)
        => channelId >= 0 && channelId < _channels.Count ? _channels[channelId] : null;
}
