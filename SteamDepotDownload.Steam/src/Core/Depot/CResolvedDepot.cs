using SteamDepotDownload.Steam.Shared.Depot;

namespace SteamDepotDownload.Steam.Core.Depot;

internal sealed record CResolvedDepot
{
    public required uint DepotId { get; init; }

    public required uint AppId { get; init; }

    public required ulong ManifestId { get; init; }

    public required string Branch { get; init; }

    public required string InstallDirectory { get; init; }

    public required byte[] DepotKey { get; init; }

    public uint BuildId { get; init; }

    public string? Name { get; init; }

    public string ConfigDirectory => Path.Combine(InstallDirectory, DepotConstants.ConfigDirectory);

    public string ManifestDirectory => Path.Combine(ConfigDirectory, DepotConstants.ManifestDirectory);

    public string DumpDirectory => Path.Combine(ConfigDirectory, DepotConstants.DumpDirectory);

    public string StagingDirectory => Path.Combine(ConfigDirectory, DepotConstants.StagingDirectory);
}
