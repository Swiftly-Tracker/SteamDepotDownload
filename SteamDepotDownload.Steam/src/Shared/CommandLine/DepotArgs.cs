using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Session;

namespace SteamDepotDownload.Steam.Shared.CommandLine;

public enum DownloadTargetKind
{
    None,
    App,
    Pubfile,
    Ugc,
}

public sealed record DepotArgs
{
    public required SteamCredentials Credentials { get; init; }

    public required SteamSessionOptions SessionOptions { get; init; }

    public required DownloadConfig DownloadConfig { get; init; }

    public DownloadTargetKind Target { get; init; }

    public uint AppId { get; init; }

    public AppDownloadRequest? Request { get; init; }

    public ulong PublishedFileId { get; init; }

    public ulong UgcId { get; init; }

    public bool ShowVersion { get; init; }

    public bool ShowHelp { get; init; }

    /// <remarks>
    /// Answered from the install directory alone, so it takes precedence over any download target
    /// rather than adding one — nothing here needs a Steam session.
    /// </remarks>
    public bool ShowStatus { get; init; }

    public bool Debug { get; init; }

    public IReadOnlyList<string> UnknownArguments { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public bool HasTarget => Target != DownloadTargetKind.None;
}
