namespace SteamDepotDownload.Tier0.Shared.Logging;

public interface ILoggingListener
{
    void Log(LoggingContext context, string message);

    void OnFlush() { }

    void OnChannelRegistered(int channelId) { }

    void OnChannelVerbosityChanged(int channelId) { }

    void OnChannelFlagsChanged(int channelId) { }
}
