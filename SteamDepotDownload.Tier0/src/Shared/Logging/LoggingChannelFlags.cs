namespace SteamDepotDownload.Tier0.Shared.Logging;

[Flags]
public enum LoggingChannelFlags
{
    None = 0,
    ConsoleOnly = 1 << 0,
    DoNotEcho = 1 << 1,
}
