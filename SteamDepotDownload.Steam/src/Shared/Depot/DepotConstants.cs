namespace SteamDepotDownload.Steam.Shared.Depot;

public static class DepotConstants
{
    public const uint InvalidAppId = uint.MaxValue;

    public const uint InvalidDepotId = uint.MaxValue;

    public const ulong InvalidManifestId = ulong.MaxValue;

    public const string PublicBranch = "public";

    public const string DefaultLanguage = "english";

    public static readonly string ConfigDirectory = ".sdd";

    public static readonly string ManifestDirectory = "manifests";

    public static readonly string DumpDirectory = "dumps";

    public static readonly string StagingDirectory = "staging";

    public static readonly string DepotStateFile = "state.sdb";

    public static readonly string ManifestExtension = ".sdm";

    public static readonly string DefaultDownloadDirectory = "depots";
}
