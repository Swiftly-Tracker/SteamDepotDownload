using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Auth;
using SteamDepotDownload.Steam.Shared.CommandLine;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Session;
using SteamDepotDownload.Tier0.Shared.CommandLine;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Logging;
using SteamDepotDownload.Tier0.Shared.Terminal;

public class Application
{
    public static async Task<int> Main(string[] args)
    {
        var previousEncoding = Console.OutputEncoding;
        ConsoleEncoding.EnsureUtf8();

        try
        {
            return await RunAsync(args).ConfigureAwait(false);
        }
        finally
        {
            ConsoleEncoding.Restore(previousEncoding);
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        InterfaceSystem.LoadModule("SteamDepotDownload.Tier0");
        InterfaceSystem.LoadModule("SteamDepotDownload.Steam");

        InterfaceSystem.GetInterface<ICommandLine>(InterfaceNames.CommandLine)!.Initialize(args);

        var parsed = DepotArgsParser.Parse(args, DepotDefaults.FromConVars(),
            AccountSettingsStore.CreateDefault());

        if (parsed.ShowVersion)
        {
            Console.WriteLine(DepotUsage.VersionLine);
            return 0;
        }

        if (parsed.ShowHelp)
        {
            Console.WriteLine(DepotUsage.Text);
            return 0;
        }

        foreach (var warning in parsed.Warnings)
        {
            Console.Error.WriteLine($"warning: {warning}");
        }

        if (parsed.Errors.Count > 0)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine($"error: {error}");
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("Run with -help for the full list of options.");
            return 1;
        }

        if (parsed.ShowStatus)
        {
            return ReportStatus(parsed);
        }

        return parsed.HasTarget
            ? await RunOneShotAsync(parsed).ConfigureAwait(false)
            : RunTerminal();
    }

    private static int ReportStatus(DepotArgs parsed)
    {
        var directory = parsed.DownloadConfig.InstallDirectory;
        var sites = DepotState.Discover(directory);
        var appId = parsed.Target == DownloadTargetKind.App ? parsed.AppId : (uint?)null;

        var lines = DepotState.Describe(sites, appId).ToList();

        if (lines.Count == 0)
        {
            Console.Error.WriteLine(directory == null
                ? $"Nothing installed under {DepotState.ScanRoot}."
                : $"Nothing installed in {Path.GetFullPath(directory)}.");

            return 1;
        }

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    private static int RunTerminal()
    {
        var terminal = InterfaceSystem.GetInterface<ITerminal>(InterfaceNames.Terminal);

        if (terminal == null)
        {
            Console.Error.WriteLine("The terminal is unavailable. Run with -help for command-line usage.");
            return 1;
        }

        terminal.Run();
        return 0;
    }

    private static async Task<int> RunOneShotAsync(DepotArgs parsed)
    {
        foreach (var argument in parsed.UnknownArguments)
        {
            Console.Error.WriteLine($"warning: ignoring unrecognised argument '{argument}'.");
        }

        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = !cancellation.IsCancellationRequested;
            cancellation.Cancel();
        };

        var options = parsed.SessionOptions with
        {
            Authenticator = Authenticators.CreateConsole(parsed.Credentials.PreferTwoFactorCode),
        };

        if (parsed.Debug)
        {
            EnableDebugLogging();
        }

        ISteamSession? session = null;

        try
        {
            session = await SteamClientFactory.Create()
                .ConnectAsync(parsed.Credentials, options, cancellation.Token)
                .ConfigureAwait(false);

            var downloader = session.CreateDownloader(parsed.DownloadConfig with
            {
                RemoveUnusedFiles = DepotDefaults.RemoveUnusedFiles,
            });

            var progress = new Progress<DownloadProgress>(CreateProgressReporter());

            var result = parsed.Target switch
            {
                DownloadTargetKind.Pubfile => await downloader
                    .DownloadPubfileAsync(parsed.PublishedFileId, progress, cancellation.Token)
                    .ConfigureAwait(false),
                DownloadTargetKind.Ugc => await downloader
                    .DownloadUgcAsync(parsed.AppId, parsed.UgcId, progress, cancellation.Token)
                    .ConfigureAwait(false),
                _ => await downloader
                    .DownloadAppAsync(parsed.Request!, progress, cancellation.Token)
                    .ConfigureAwait(false),
            };

            Summarize(result);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (DepotDownloadException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        finally
        {
            if (session != null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (CProfiler.Enabled)
            {
                CProfiler.PrintSummary();
            }
        }
    }

    private static void EnableDebugLogging()
    {
        CProfiler.Enabled = true;

        var logging = InterfaceSystem.GetInterface<ILoggingSystem>(InterfaceNames.LoggingSystem);
        if (logging == null)
        {
            return;
        }

        foreach (var channel in new[] { "Steam", "Depot", "CDN", "General", "Developer" })
        {
            logging.SetChannelVerbosityByName(channel, LoggingVerbosity.Detailed);
        }
    }

    private static Action<DownloadProgress> CreateProgressReporter()
    {
        if (!Console.IsOutputRedirected)
        {
            return static _ => { };
        }

        var lastPrint = DateTime.MinValue;
        var lastFraction = -1.0;

        uint? stageDepotId = null;
        var stageStart = DateTime.MinValue;
        var stageStartBytes = 0UL;

        return progress =>
        {
            var now = DateTime.UtcNow;
            var stalled = now - lastPrint >= TimeSpan.FromSeconds(5);
            var advanced = progress.Fraction - lastFraction >= 0.05;

            if (stageDepotId != progress.DepotId)
            {
                stageDepotId = progress.DepotId;
                stageStart = now;
                stageStartBytes = progress.BytesDownloaded;
            }

            if (!stalled && !advanced && progress.Fraction < 1.0)
            {
                return;
            }

            lastPrint = now;
            lastFraction = progress.Fraction;

            var line = progress.CurrentFile ?? progress.Stage ?? string.Empty;
            var eta = FormatEta(progress, now - stageStart, stageStartBytes);

            Console.WriteLine(
                $"[{progress.Fraction,7:P1}] {FormatBytes(progress.BytesDownloaded)} / {FormatBytes(progress.BytesTotal)}" +
                $"{eta}  {line}");
        };
    }

    private static string FormatEta(DownloadProgress progress, TimeSpan sinceStageStart, ulong stageStartBytes)
    {
        if (progress.BytesTotal == 0 || progress.Fraction >= 1.0 || sinceStageStart <= TimeSpan.Zero)
        {
            return string.Empty;
        }

        var downloadedThisStage = progress.BytesDownloaded - stageStartBytes;
        var bytesPerSecond = downloadedThisStage / sinceStageStart.TotalSeconds;

        if (bytesPerSecond <= 0)
        {
            return string.Empty;
        }

        var remaining = progress.BytesTotal - progress.BytesDownloaded;
        var etaSeconds = remaining / bytesPerSecond;

        var eta = TimeSpan.FromSeconds(Math.Min(etaSeconds, TimeSpan.MaxValue.TotalSeconds));
        var etaText = eta.TotalHours >= 1
            ? $"{(int)eta.TotalHours}h {eta.Minutes}m"
            : eta.TotalMinutes >= 1
                ? $"{eta.Minutes}m {eta.Seconds}s"
                : $"{eta.Seconds}s";

        return $"  eta {etaText} ({FormatBytes((ulong)bytesPerSecond)}/s)";
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }

    private static void Summarize(DownloadResult result)
    {
        if (!Console.IsOutputRedirected)
        {
            Console.WriteLine();
        }

        foreach (var depot in result.Depots)
        {
            if (depot.ManifestDumpPath != null)
            {
                Console.WriteLine($"depot {depot.DepotId}: wrote {depot.ManifestDumpPath}");
                continue;
            }

            if (depot.AlreadyInstalled)
            {
                Console.WriteLine($"depot {depot.DepotId}: already at manifest {depot.ManifestId}");
                continue;
            }

            Console.WriteLine($"depot {depot.DepotId}: {depot.FilesDownloaded} files written, " +
                $"{depot.FilesSkipped} already current -> {depot.InstallDirectory}");
        }

        if (result.BytesDownloaded > 0)
        {
            Console.WriteLine($"{result.BytesDownloaded} bytes in {result.Elapsed.TotalSeconds:0.0}s");
        }
    }
}
