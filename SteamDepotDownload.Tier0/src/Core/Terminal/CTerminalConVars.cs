using SteamDepotDownload.Tier0.Shared.ConVar;

namespace SteamDepotDownload.Tier0.Core.Terminal;

internal static class CTerminalConVars
{
    private static ConVar<int>? _maxTasks;

    /// <summary>
    /// How many progress tasks the terminal draws at once. Tasks beyond this still run — they are
    /// simply not shown.
    /// </summary>
    /// <remarks>
    /// Every drawn task costs a row, and the layout gives the remaining rows to the log, so this is
    /// a trade rather than a limit worth removing. It stopped being a constant when work started
    /// fanning out across processes: with one task per worker, a pool larger than the old six left
    /// workers running invisibly.
    /// </remarks>
    internal static int MaxTasks => _maxTasks?.Value ?? 8;

    internal static void Register()
    {
        _maxTasks ??= new ConVar<int>(
            "terminal_max_tasks",
            8,
            "How many progress bars the terminal draws at once. Tasks past this still run, they " +
            "are just not drawn; each one drawn costs a row of log scrollback.",
            ConVarFlags.None,
            (1, 32));
    }
}
