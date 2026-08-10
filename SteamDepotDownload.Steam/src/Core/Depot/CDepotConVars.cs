using SteamDepotDownload.Steam.Shared.CommandLine;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Tier0.Shared.ConVar;
using SteamDepotDownload.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Steam.Core.Depot;

internal static class CDepotConVars
{
    private static ConVar<string>? _dir;
    private static ConVar<int>? _maxDownloads;
    private static ConVar<int>? _cellId;
    private static ConVar<string>? _branch;
    private static ConVar<string>? _branchPassword;
    private static ConVar<string>? _language;
    private static ConVar<string>? _os;
    private static ConVar<string>? _osArch;
    private static ConVar<string>? _username;
    private static ConVar<string>? _fileList;
    private static ConVar<bool>? _validate;
    private static ConVar<bool>? _manifestOnly;
    private static ConVar<bool>? _allPlatforms;
    private static ConVar<bool>? _allArchs;
    private static ConVar<bool>? _allLanguages;
    private static ConVar<bool>? _lowViolence;
    private static ConVar<bool>? _useLancache;
    private static ConVar<bool>? _rememberPassword;
    private static ConVar<bool>? _noMobile;
    private static ConVar<bool>? _removeUnused;
    private static ConVar<bool>? _debug;
    private static ConVar<int>? _loginId;

    internal static void Register()
    {
        _dir ??= new ConVar<string>("depot_dir", string.Empty,
            "Where downloads are written. Empty uses depots/<depot>/<build> under the working directory.");

        _maxDownloads ??= new ConVar<int>("depot_max_downloads", 32,
            "How many chunks to fetch at once. Higher saturates fast links; lower is kinder to slow ones.",
            ConVarFlags.None, (1, 64));

        _cellId ??= new ConVar<int>("depot_cellid", 0,
            "Overrides which Steam cell content servers are picked from. 0 uses the one Steam assigns.",
            ConVarFlags.None, (0, int.MaxValue));

        _branch ??= new ConVar<string>("depot_branch", DepotConstants.PublicBranch,
            "Branch to download from.");

        _branchPassword ??= new ConVar<string>("depot_branch_password", string.Empty,
            "Password for a private branch.");

        _language ??= new ConVar<string>("depot_language", DepotConstants.DefaultLanguage,
            "Which language depots to take.");

        _os ??= new ConVar<string>("depot_os", string.Empty,
            "Target operating system: windows, macos or linux. Empty uses this host's.");

        _osArch ??= new ConVar<string>("depot_osarch", string.Empty,
            "Target architecture: 32 or 64. Empty uses this host's.");

        _username ??= new ConVar<string>("depot_username", string.Empty,
            "Account to log in as when no -username is given.");

        _fileList ??= new ConVar<string>("depot_filelist", string.Empty,
            "Path to a file list limiting which files are downloaded. One path per line, " +
            "prefixed with regex: to match a pattern, or a JSON object of depot id to a list of " +
            "rules, which also picks the depots. Empty downloads everything.");

        _validate ??= new ConVar<bool>("depot_validate", false,
            "Checksum files that are already on disk instead of trusting the previous manifest.");

        _manifestOnly ??= new ConVar<bool>("depot_manifest_only", false,
            "Write a readable manifest listing instead of downloading content.");

        _allPlatforms ??= new ConVar<bool>("depot_all_platforms", false,
            "Take every platform's depots rather than this host's.");

        _allArchs ??= new ConVar<bool>("depot_all_archs", false,
            "Take every architecture's depots rather than this host's.");

        _allLanguages ??= new ConVar<bool>("depot_all_languages", false,
            "Take every language's depots rather than the one in depot_language.");

        _lowViolence ??= new ConVar<bool>("depot_lowviolence", false,
            "Include low violence depots.");

        _useLancache ??= new ConVar<bool>("depot_use_lancache", false,
            "Route downloads through a Lancache instance on the local network.");

        _rememberPassword ??= new ConVar<bool>("depot_remember_password", false,
            "Persist a refresh token so later runs do not prompt.");

        _noMobile ??= new ConVar<bool>("depot_no_mobile", false,
            "Ask for a Steam Guard code instead of waiting for approval in the mobile app.");

        _removeUnused ??= new ConVar<bool>("depot_remove_unused", false,
            "Delete files in the install directory that no downloaded manifest claims.");

        _debug ??= new ConVar<bool>("depot_debug", false,
            "Turn the Steam, Depot and CDN log channels up to Detailed.");

        _loginId ??= new ConVar<int>("depot_loginid", 0,
            "Unique login id, needed to run several sessions at once. 0 lets Steam choose.",
            ConVarFlags.None, (0, int.MaxValue));
    }

    internal static bool RemoveUnusedFiles => _removeUnused?.Value ?? false;

    internal static DepotArgsDefaults ToDefaults() => new()
    {
        InstallDirectory = Empty(_dir?.Value),
        MaxDownloads = _maxDownloads?.Value ?? 32,
        CellId = (uint)Math.Max(0, _cellId?.Value ?? 0),
        Branch = Empty(_branch?.Value) ?? DepotConstants.PublicBranch,
        BranchPassword = Empty(_branchPassword?.Value),
        Language = Empty(_language?.Value) ?? DepotConstants.DefaultLanguage,
        Os = Empty(_os?.Value),
        Arch = Empty(_osArch?.Value),
        Username = Empty(_username?.Value),
        FileList = Empty(_fileList?.Value),
        Validate = _validate?.Value ?? false,
        ManifestOnly = _manifestOnly?.Value ?? false,
        AllPlatforms = _allPlatforms?.Value ?? false,
        AllArchitectures = _allArchs?.Value ?? false,
        AllLanguages = _allLanguages?.Value ?? false,
        LowViolence = _lowViolence?.Value ?? false,
        UseLancache = _useLancache?.Value ?? false,
        RememberPassword = _rememberPassword?.Value ?? false,
        PreferTwoFactorCode = _noMobile?.Value ?? false,
        Debug = _debug?.Value ?? false,
        LoginId = _loginId is { Value: > 0 } id ? (uint)id.Value : null,
        HostFlags = RegisteredConVarNames(),
    };

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlySet<string> RegisteredConVarNames()
    {
        var system = InterfaceSystem.GetInterface<IConVarSystem>(InterfaceNames.ConVar);

        return system == null
            ? new HashSet<string>()
            : new HashSet<string>(system.GetAll().Select(convar => convar.Name), StringComparer.OrdinalIgnoreCase);
    }
}
