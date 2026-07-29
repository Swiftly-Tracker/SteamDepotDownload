using SteamDepotDownload.Steam.Core.Depot;

namespace SteamDepotDownload.Steam.Shared.CommandLine;

/// <summary>
/// Reads the <c>depot_*</c> ConVars into the parser's defaults.
/// </summary>
/// <remarks>
/// Safe to call when no Tier0 module has been loaded: the ConVars simply do not exist yet and the
/// built-in defaults come back instead.
/// </remarks>
public static class DepotDefaults
{
    public static DepotArgsDefaults FromConVars() => CDepotConVars.ToDefaults();

    public static bool RemoveUnusedFiles => CDepotConVars.RemoveUnusedFiles;
}
