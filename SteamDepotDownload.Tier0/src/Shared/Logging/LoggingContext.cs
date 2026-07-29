using SteamDepotDownload.Tier0.Shared.Drawing;

namespace SteamDepotDownload.Tier0.Shared.Logging;

public sealed class LoggingContext
{
    public required int ChannelId { get; init; }
    public required string ChannelName { get; init; }
    public required LoggingChannelFlags Flags { get; init; }
    public required LoggingSeverity Severity { get; init; }
    public required LoggingVerbosity Verbosity { get; init; }
    public required Color Color { get; init; }
    public LeafCodeInfo Source { get; init; }
}
