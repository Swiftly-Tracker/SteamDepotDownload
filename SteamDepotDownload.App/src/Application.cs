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

            var progress = new Progress<DownloadProgress>(ReportProgress);

            var result = await downloader
                .DownloadAppAsync(parsed.Request!, progress, cancellation.Token)
                .ConfigureAwait(false);

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
        }
    }

    private static void EnableDebugLogging()
    {
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

    private static void ReportProgress(DownloadProgress progress)
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        var line = progress.CurrentFile ?? progress.Stage ?? string.Empty;
        var width = Math.Max(20, Console.WindowWidth - 12);

        if (line.Length > width)
        {
            line = "..." + line[^(width - 3)..];
        }

        Console.Write($"\r{progress.Fraction,7:P1} {line}".PadRight(width + 8));
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
