using SteamDepotDownload.Tier0.Shared.ConVar;
using SteamDepotDownload.Tier0.Shared.Logging;

namespace SteamDepotDownload.Tier0.Core.Logging;

internal sealed class CLoggingConVars : ILoggingListener
{
    private readonly ILoggingSystem _log;
    private readonly Dictionary<int, ConVar<LoggingVerbosity>> _verbosityConVars = [];
    private readonly Dictionary<int, ConVar<LoggingChannelFlags>> _flagsConVars = [];
    private bool _syncing;

    public CLoggingConVars(ILoggingSystem log)
    {
        _log = log;
    }

    public void Log(LoggingContext context, string message)
    {
    }

    public void OnChannelRegistered(int channelId)
    {
        string? channelName = _log.GetChannelName(channelId);
        if (channelName == null)
        {
            return;
        }

        string slug = channelName.ToLowerInvariant();

        var verbosity = new ConVar<LoggingVerbosity>(
            $"log_{slug}",
            _log.GetChannelVerbosity(channelId),
            $"Verbosity of the '{channelName}' log channel (Off, Essential, Default, Detailed, Max).");

        verbosity.OnChanged += (_, _, value) =>
        {
            _syncing = true;
            _log.SetChannelVerbosity(channelId, value);
            _syncing = false;
        };

        var flags = new ConVar<LoggingChannelFlags>(
            $"log_{slug}_flags",
            _log.GetChannelFlags(channelId),
            $"Flags of the '{channelName}' log channel (None, ConsoleOnly, DoNotEcho).");

        flags.OnChanged += (_, _, value) =>
        {
            _syncing = true;
            _log.SetChannelFlags(channelId, value);
            _syncing = false;
        };

        _verbosityConVars[channelId] = verbosity;
        _flagsConVars[channelId] = flags;
    }

    public void OnChannelVerbosityChanged(int channelId)
    {
        if (_syncing)
        {
            return;
        }

        if (_verbosityConVars.TryGetValue(channelId, out var convar))
        {
            convar.Value = _log.GetChannelVerbosity(channelId);
        }
    }

    public void OnChannelFlagsChanged(int channelId)
    {
        if (_syncing)
        {
            return;
        }

        if (_flagsConVars.TryGetValue(channelId, out var convar))
        {
            convar.Value = _log.GetChannelFlags(channelId);
        }
    }

    public void CreateGlobalConVar()
    {
        var convar = new ConVar<LoggingVerbosity>(
            "log_level",
            LoggingVerbosity.Default,
            "Sets the verbosity of every log channel at once (Off, Essential, Default, Detailed, Max).");

        convar.OnChanged += (_, _, value) =>
        {
            for (int id = 0; id < _log.ChannelCount; id++)
            {
                _log.SetChannelVerbosity(id, value);
            }
        };
    }
}
