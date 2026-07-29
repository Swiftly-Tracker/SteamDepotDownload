namespace SteamDepotDownload.Tier0.Shared.Logging;

public interface ILoggingResponsePolicy
{
    LoggingResponse OnLog(LoggingContext context);
}
