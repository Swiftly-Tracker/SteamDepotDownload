using System.Runtime.CompilerServices;
using SteamDepotDownload.Tier0.Shared.Drawing;

namespace SteamDepotDownload.Tier0.Shared.Logging;

public interface ILoggingSystem
{
    const int MaxChannelCount = 256;

    const int MaxIdentifierLength = 32;

    int RegisterChannel(string name, LoggingChannelFlags flags = LoggingChannelFlags.None,
        LoggingVerbosity verbosity = LoggingVerbosity.Default, Color color = default);

    int FindChannel(string name);

    string? GetChannelName(int channelId);

    int ChannelCount { get; }

    void AddTagToChannel(int channelId, string tag);

    bool HasTag(int channelId, string tag);

    bool IsChannelEnabled(int channelId, LoggingVerbosity verbosity);

    bool IsChannelEnabled(int channelId, LoggingSeverity severity);

    LoggingVerbosity GetChannelVerbosity(int channelId);

    void SetChannelVerbosity(int channelId, LoggingVerbosity verbosity);

    void SetChannelVerbosityByName(string name, LoggingVerbosity verbosity);

    void SetChannelVerbosityByTag(string tag, LoggingVerbosity verbosity);

    Color GetChannelColor(int channelId);

    void SetChannelColor(int channelId, Color color);

    LoggingChannelFlags GetChannelFlags(int channelId);

    void SetChannelFlags(int channelId, LoggingChannelFlags flags);

    void RegisterListener(ILoggingListener listener);

    void UnregisterListener(ILoggingListener listener);

    bool IsListenerRegistered(ILoggingListener listener);

    void SetResponsePolicy(ILoggingResponsePolicy? policy);

    ILoggingTask BeginSpinner(int channelId, string label,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null);

    ILoggingTask BeginProgress(int channelId, string label,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null);

    IReadOnlyList<ILoggingTask> ActiveTasks { get; }

    LoggingResponse Log(int channelId, LoggingSeverity severity, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null);

    LoggingResponse Log(int channelId, LoggingSeverity severity, Color color, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null);

    LoggingResponse Msg(int channelId, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => Log(channelId, LoggingSeverity.Message, message, file, line, function);

    LoggingResponse DetailedMsg(int channelId, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => Log(channelId, LoggingSeverity.Detailed, message, file, line, function);

    LoggingResponse Warning(int channelId, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => Log(channelId, LoggingSeverity.Warning, message, file, line, function);

    LoggingResponse Error(int channelId, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => Log(channelId, LoggingSeverity.Error, message, file, line, function);

    LoggingResponse Assert(int channelId, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => Log(channelId, LoggingSeverity.Assert, message, file, line, function);
}
