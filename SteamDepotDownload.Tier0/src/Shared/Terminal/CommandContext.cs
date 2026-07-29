using System.Runtime.CompilerServices;

namespace SteamDepotDownload.Tier0.Shared.Terminal;

public delegate void CommandPrint(string message,
    [CallerFilePath] string? file = null,
    [CallerLineNumber] int line = 0,
    [CallerMemberName] string? function = null);

public sealed class CommandContext
{
    public required string Name { get; init; }
    public required string[] Args { get; init; }
    public required string ArgString { get; init; }
    public required ITerminal Terminal { get; init; }
    public required CommandPrint Print { get; init; }
    public required CommandPrint Warn { get; init; }
}
