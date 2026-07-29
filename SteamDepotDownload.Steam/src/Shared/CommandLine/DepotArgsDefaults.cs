using SteamDepotDownload.Steam.Shared.Depot;

namespace SteamDepotDownload.Steam.Shared.CommandLine;

public sealed record DepotArgsDefaults
{
    public string? InstallDirectory { get; init; }

    public string Branch { get; init; } = DepotConstants.PublicBranch;

    public string? BranchPassword { get; init; }

    public string Language { get; init; } = DepotConstants.DefaultLanguage;

    public string? Os { get; init; }

    public string? Arch { get; init; }

    public string? Username { get; init; }

    public string? FileList { get; init; }

    public int MaxDownloads { get; init; } = 8;

    public uint CellId { get; init; }

    public bool Validate { get; init; }

    public bool ManifestOnly { get; init; }

    public bool AllPlatforms { get; init; }

    public bool AllArchitectures { get; init; }

    public bool AllLanguages { get; init; }

    public bool LowViolence { get; init; }

    public bool UseLancache { get; init; }

    public bool RememberPassword { get; init; }

    public bool PreferTwoFactorCode { get; init; }

    public bool Debug { get; init; }

    public uint? LoginId { get; init; }

    public IReadOnlySet<string> HostFlags { get; init; } = new HashSet<string>();

    public static DepotArgsDefaults Standard { get; } = new();
}
