using SteamDepotDownload.Steam.Core.Auth;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Auth;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Interfaces;
using SteamDepotDownload.Steam.Shared.Jobs;
using SteamDepotDownload.Steam.Shared.Session;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Terminal;

namespace SteamDepotDownload.Steam.Core.Depot;

internal static class CDepotCommands
{
    private static IDownloadJobs? _jobs;

    internal static void Register()
    {
        _ = new ConCommand("steam_login", Login,
            "Log in: steam_login <username> [password]. Prompts through steam_code when needed.");
        _ = new ConCommand("steam_login_qr", LoginQr,
            "Log in by scanning a QR code with the Steam mobile app.");
        _ = new ConCommand("steam_login_anonymous", LoginAnonymous,
            "Log in anonymously. Enough for free and redistributable content.");
        _ = new ConCommand("steam_logout", Logout, "Close the current Steam session.");
        _ = new ConCommand("steam_code", SupplyCode,
            "Answer whatever the pending login is asking for: steam_code <value>.");
        _ = new ConCommand("steam_status", Status, "Show the current session.");

        _ = new ConCommand("app_info", AppInfo, "Show an app's name, branches and depots: app_info <appid>.");
        _ = new ConCommand("depot_list", DepotList,
            "List the depots a download would take: depot_list <appid>.");
        _ = new ConCommand("manifest_dump", ManifestDump,
            "Write a readable manifest: manifest_dump <appid> <depotid> [manifestid].");
        _ = new ConCommand("depot_status", DepotStatus,
            "Show what is installed on disk: depot_status [directory]. Reads local state, no Steam.");

        _ = new ConCommand("download_app", DownloadApp,
            "Download an app: download_app <appid> [depotid [manifestid]] ...");

        _ = new ConCommand("download_status", DownloadStatus, "List download jobs and their progress.");
        _ = new ConCommand("download_cancel", DownloadCancel,
            "Cancel a download job: download_cancel <id>, or with no id, all of them.");
    }

    private static IDownloadJobs Jobs
        => _jobs ??= InterfaceSystem.GetInterface<IDownloadJobs>(SteamInterfaceNames.DownloadJobs)
            ?? throw new DepotDownloadException("The download job registry is not available.");

    private static void Login(CommandContext ctx)
    {
        if (ctx.Args.Length == 0)
        {
            ctx.Warn("Usage: steam_login <username> [password]");
            return;
        }

        var credentials = new SteamCredentials
        {
            Username = ctx.Args[0],
            PlainPassword = ctx.Args.Length > 1 ? ctx.Args[1] : null,
            RememberPassword = CDepotConVars.ToDefaults().RememberPassword,
            PreferTwoFactorCode = CDepotConVars.ToDefaults().PreferTwoFactorCode,
            LoginId = CDepotConVars.ToDefaults().LoginId,
        };

        StartLogin(ctx, credentials, $"logging in as {ctx.Args[0]}");
    }

    private static void LoginQr(CommandContext ctx)
        => StartLogin(ctx, SteamCredentials.QrCode with
        {
            RememberPassword = CDepotConVars.ToDefaults().RememberPassword,
            PreferTwoFactorCode = CDepotConVars.ToDefaults().PreferTwoFactorCode,
        }, "logging in by QR code");

    private static void LoginAnonymous(CommandContext ctx)
        => StartLogin(ctx, SteamCredentials.Anonymous, "logging in anonymously");

    private static void StartLogin(CommandContext ctx, SteamCredentials credentials, string label)
    {
        var defaults = CDepotConVars.ToDefaults();

        var options = new SteamSessionOptions
        {
            Authenticator = Authenticators.CreateTerminal(defaults.PreferTwoFactorCode),
            CellIdOverride = defaults.CellId == 0 ? null : defaults.CellId,
            UseLancache = defaults.UseLancache,
        };

        var gate = CSessionHolder.BeginLogin();

        var id = Jobs.Start(label, async (_, ct) =>
        {
            try
            {
                await CSessionHolder.LoginAsync(credentials, options, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.TrySetResult();
            }
        });

        ctx.Print($"[{id}] {label}.");
    }

    private static void Logout(CommandContext ctx)
    {
        CTerminalAuthenticator.CancelPending();
        CSessionHolder.LogoutAsync().GetAwaiter().GetResult();
        ctx.Print("Logged out.");
    }

    private static void SupplyCode(CommandContext ctx)
    {
        if (ctx.Args.Length == 0)
        {
            var prompt = CTerminalAuthenticator.PendingPrompt;
            ctx.Warn(prompt == null
                ? "Nothing is waiting for input."
                : $"Waiting for the {prompt}. Supply it with: steam_code <value>");
            return;
        }

        ctx.Print(CTerminalAuthenticator.TrySupply(ctx.ArgString)
            ? "Sent."
            : "Nothing is waiting for input.");
    }

    private static void Status(CommandContext ctx)
    {
        var session = CSessionHolder.Current;

        if (session == null)
        {
            ctx.Print("Not logged in.");
            return;
        }

        ctx.Print(session.IsAnonymous
            ? $"Logged in anonymously (cell {session.CellId})."
            : $"Logged in as {session.AccountName} ({session.SteamId}, cell {session.CellId}).");
    }

    private static void AppInfo(CommandContext ctx)
    {
        if (!TryParseApp(ctx, out var appId))
        {
            return;
        }

        Run(ctx, $"app info {appId}", async ct =>
        {
            var session = await CSessionHolder.RequireAsync(ct).ConfigureAwait(false);
            var info = await session.GetAppInfoAsync(appId, ct).ConfigureAwait(false);

            if (info == null)
            {
                ctx.Warn($"App {appId} is unknown.");
                return;
            }

            ctx.Print($"{info.Name} ({info.AppId})");

            foreach (var branch in info.Branches)
            {
                ctx.Print($"  branch {branch.Name}: build {branch.BuildId}" +
                    (branch.RequiresPassword ? " (password required)" : string.Empty));
            }

            foreach (var depot in info.Depots)
            {
                ctx.Print($"  depot {depot.DepotId}: {depot.Name ?? "(unnamed)"}" +
                    $"{Describe(depot)}{DescribeSize(depot)}");
            }
        });
    }

    private static void DepotList(CommandContext ctx)
    {
        if (!TryParseApp(ctx, out var appId))
        {
            return;
        }

        Run(ctx, $"depot list {appId}", async ct =>
        {
            var session = await CSessionHolder.RequireAsync(ct).ConfigureAwait(false);
            var downloader = session.CreateDownloader(BuildConfig());
            var depots = await downloader.ResolveDepotsAsync(BuildRequest(appId, []), ct).ConfigureAwait(false);

            if (depots.Count == 0)
            {
                ctx.Warn($"No depots of app {appId} match the current filters.");
                return;
            }

            foreach (var depot in depots)
            {
                ctx.Print($"depot {depot.DepotId} manifest {depot.ManifestId}" +
                    $"{Describe(depot)}{DescribeSize(depot)} — {depot.Name ?? "(unnamed)"}");
            }
        });
    }

    private static void ManifestDump(CommandContext ctx)
    {
        if (ctx.Args.Length < 2 ||
            !uint.TryParse(ctx.Args[0], out var appId) ||
            !uint.TryParse(ctx.Args[1], out var depotId))
        {
            ctx.Warn("Usage: manifest_dump <appid> <depotid> [manifestid]");
            return;
        }

        var manifestId = ctx.Args.Length > 2 && ulong.TryParse(ctx.Args[2], out var parsed)
            ? parsed
            : DepotConstants.InvalidManifestId;

        Run(ctx, $"manifest dump {depotId}", async ct =>
        {
            var session = await CSessionHolder.RequireAsync(ct).ConfigureAwait(false);
            var downloader = session.CreateDownloader(BuildConfig());

            var path = await downloader
                .DumpManifestAsync(appId, depotId, manifestId, CDepotConVars.ToDefaults().Branch, ct)
                .ConfigureAwait(false);

            ctx.Print($"Wrote {path}");
        });
    }

    private static void DepotStatus(CommandContext ctx)
    {
        var directory = ctx.Args.Length > 0
            ? ctx.ArgString
            : CDepotConVars.ToDefaults().InstallDirectory;

        var sites = DepotState.Discover(directory);

        if (sites.Count == 0)
        {
            ctx.Warn(directory == null
                ? $"Nothing installed under {DepotState.ScanRoot}."
                : $"Nothing installed in {Path.GetFullPath(directory)}.");

            return;
        }

        foreach (var line in DepotState.Describe(sites))
        {
            ctx.Print(line);
        }
    }

    private static void DownloadApp(CommandContext ctx)
    {
        if (!TryParseApp(ctx, out var appId))
        {
            ctx.Warn("Usage: download_app <appid> [depotid [manifestid]] ...");
            return;
        }

        var selectors = new List<DepotSelector>();

        for (var i = 1; i < ctx.Args.Length; i++)
        {
            if (!uint.TryParse(ctx.Args[i], out var depotId))
            {
                ctx.Warn($"'{ctx.Args[i]}' is not a depot id.");
                return;
            }

            var manifestId = DepotConstants.InvalidManifestId;

            if (i + 1 < ctx.Args.Length && ulong.TryParse(ctx.Args[i + 1], out var parsed) && parsed > uint.MaxValue)
            {
                manifestId = parsed;
                i++;
            }

            selectors.Add(new DepotSelector(depotId, manifestId));
        }

        DownloadConfig config;

        try
        {
            config = BuildConfig();
        }
        catch (DepotDownloadException ex)
        {
            ctx.Warn(ex.Message);
            return;
        }

        var request = BuildRequest(appId, selectors);

        var id = Jobs.Start($"download app {appId}", async (progress, ct) =>
        {
            var session = await CSessionHolder.RequireAsync(ct).ConfigureAwait(false);
            await session.CreateDownloader(config).DownloadAppAsync(request, progress, ct).ConfigureAwait(false);
        });

        ctx.Print($"[{id}] downloading app {appId}.");
    }

    private static void DownloadStatus(CommandContext ctx)
    {
        var jobs = Jobs.GetJobs();

        if (jobs.Count == 0)
        {
            ctx.Print("No jobs.");
            return;
        }

        foreach (var job in jobs)
        {
            var detail = job.Error ?? job.Detail;

            ctx.Print($"[{job.Id}] {job.State,-9} {job.Fraction,6:P0}  {job.Label}" +
                (detail == null ? string.Empty : $" — {detail}"));
        }
    }

    private static void DownloadCancel(CommandContext ctx)
    {
        if (ctx.Args.Length == 0)
        {
            Jobs.CancelAll();
            ctx.Print("Cancelling all jobs.");
            return;
        }

        if (!int.TryParse(ctx.Args[0], out var id))
        {
            ctx.Warn("Usage: download_cancel [id]");
            return;
        }

        ctx.Print(Jobs.Cancel(id) ? $"Cancelling job {id}." : $"Job {id} is not running.");
    }

    private static void Run(CommandContext ctx, string label, Func<CancellationToken, Task> work)
        => Jobs.Start(label, async (_, ct) => await work(ct).ConfigureAwait(false));

    private static bool TryParseApp(CommandContext ctx, out uint appId)
    {
        appId = 0;

        if (ctx.Args.Length == 0 || !uint.TryParse(ctx.Args[0], out appId))
        {
            ctx.Warn($"{ctx.Name} needs an app id.");
            return false;
        }

        return true;
    }

    private static DownloadConfig BuildConfig()
    {
        var defaults = CDepotConVars.ToDefaults();

        return new DownloadConfig
        {
            InstallDirectory = defaults.InstallDirectory,
            CellId = defaults.CellId,
            MaxDownloads = defaults.MaxDownloads,
            VerifyAll = defaults.Validate,
            ManifestOnly = defaults.ManifestOnly,
            FileFilter = LoadFileList(defaults.FileList),
            RemoveUnusedFiles = CDepotConVars.RemoveUnusedFiles,
        };
    }

    private static FileFilter? LoadFileList(string? path)
    {
        if (path == null)
        {
            return null;
        }

        try
        {
            return FileFilter.FromFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or DepotDownloadException or ArgumentException)
        {
            throw new DepotDownloadException($"Could not read depot_filelist '{path}': {ex.Message}");
        }
    }

    private static AppDownloadRequest BuildRequest(uint appId, IReadOnlyList<DepotSelector> depots)
    {
        var defaults = CDepotConVars.ToDefaults();

        return new AppDownloadRequest
        {
            AppId = appId,
            Depots = depots,
            Branch = defaults.Branch,
            BranchPassword = defaults.BranchPassword,
            Os = defaults.Os,
            Arch = defaults.Arch,
            Language = defaults.Language,
            LowViolence = defaults.LowViolence,
            AllPlatforms = defaults.AllPlatforms,
            AllArchitectures = defaults.AllArchitectures,
            AllLanguages = defaults.AllLanguages,
        };
    }

    private static string DescribeSize(DepotInfo depot)
    {
        if (depot.SizeOnDisk == 0)
        {
            return string.Empty;
        }

        var installed = CDepotFields.FormatBytes(depot.SizeOnDisk);

        return depot.DownloadSize == 0
            ? $" {installed}"
            : $" {installed} ({CDepotFields.FormatBytes(depot.DownloadSize)} download)";
    }

    private static string Describe(DepotInfo depot)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(depot.Os))
        {
            parts.Add(depot.Os);
        }

        if (!string.IsNullOrEmpty(depot.Arch))
        {
            parts.Add($"{depot.Arch}-bit");
        }

        if (!string.IsNullOrEmpty(depot.Language))
        {
            parts.Add(depot.Language);
        }

        if (depot.LowViolence)
        {
            parts.Add("low violence");
        }

        return parts.Count == 0 ? string.Empty : $" [{string.Join(", ", parts)}]";
    }
}
