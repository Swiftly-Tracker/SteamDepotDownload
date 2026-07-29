namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed record AppDownloadRequest
{
    public required uint AppId { get; init; }

    public IReadOnlyList<DepotSelector> Depots { get; init; } = [];

    public string Branch { get; init; } = DepotConstants.PublicBranch;

    public string? BranchPassword { get; init; }

    public string? Os { get; init; }

    public string? Arch { get; init; }

    public string Language { get; init; } = DepotConstants.DefaultLanguage;

    public bool LowViolence { get; init; }

    public bool AllPlatforms { get; init; }

    public bool AllArchitectures { get; init; }

    public bool AllLanguages { get; init; }
}
