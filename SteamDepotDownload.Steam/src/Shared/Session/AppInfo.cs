using SteamDepotDownload.Steam.Shared.Depot;

namespace SteamDepotDownload.Steam.Shared.Session;

public sealed record BranchInfo
{
    public required string Name { get; init; }

    public uint BuildId { get; init; }

    public bool RequiresPassword { get; init; }

    public string? Description { get; init; }
}

public sealed record AppInfo
{
    public required uint AppId { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<BranchInfo> Branches { get; init; } = [];

    public IReadOnlyList<DepotInfo> Depots { get; init; } = [];
}
