using System.Globalization;
using SteamDepotDownload.Steam.Shared.Auth;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Session;

namespace SteamDepotDownload.Steam.Shared.CommandLine;

public static class DepotArgsParser
{
    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "username", "user", "password", "pass", "loginid",
        "app", "depot", "manifest", "branch", "beta", "betabranch",
        "branchpassword", "betapassword",
        "dir", "filelist", "cellid", "max-downloads", "os", "osarch", "language",
    };

    private static readonly HashSet<string> BoolFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "remember-password", "qr", "no-mobile",
        "all-platforms", "all-archs", "all-languages", "lowviolence",
        "validate", "verify-all", "verify_all", "manifest-only", "use-lancache",
        "debug", "v", "version", "help", "?", "status",
    };

    public static DepotArgs Parse(string[] args, DepotArgsDefaults? defaults = null,
        IAccountSettingsStore? accountStore = null)
    {
        defaults ??= DepotArgsDefaults.Standard;

        var errors = new List<string>();
        var warnings = new List<string>();
        var unknown = new List<string>();

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var depotIds = new List<uint>();
        var manifestIds = new List<ulong>();

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (token.StartsWith('+'))
            {
                continue;
            }

            if (token.Length < 2 || token[0] != '-')
            {
                unknown.Add(token);
                continue;
            }

            var name = token.TrimStart('-');

            if (ValueFlags.Contains(name))
            {
                if (i + 1 >= args.Length)
                {
                    errors.Add($"-{name} needs a value.");
                    continue;
                }

                var value = args[++i];

                switch (name.ToLowerInvariant())
                {
                    case "depot":
                        if (TryParseUInt(value, out var depotId))
                        {
                            depotIds.Add(depotId);
                        }
                        else
                        {
                            errors.Add($"-depot expects a number, got '{value}'.");
                        }

                        break;

                    case "manifest":
                        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var manifestId))
                        {
                            manifestIds.Add(manifestId);
                        }
                        else
                        {
                            errors.Add($"-manifest expects a number, got '{value}'.");
                        }

                        break;

                    default:
                        values[Canonical(name)] = value;
                        break;
                }

                continue;
            }

            if (BoolFlags.Contains(name))
            {
                flags.Add(Canonical(name));
                continue;
            }

            if (defaults.HostFlags.Contains(name))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-') && !args[i + 1].StartsWith('+'))
                {
                    i++;
                }

                continue;
            }

            unknown.Add(token);
        }

        if (flags.Contains("help"))
        {
            return Minimal(defaults, showHelp: true);
        }

        if (flags.Contains("version"))
        {
            return Minimal(defaults, showVersion: true);
        }

        var username = values.GetValueOrDefault("username") ?? defaults.Username;
        var password = values.GetValueOrDefault("password");
        var useQr = flags.Contains("qr");
        var remember = flags.Contains("remember-password") || defaults.RememberPassword;

        if (useQr && !string.IsNullOrEmpty(username))
        {
            errors.Add("-qr cannot be combined with -username; a QR login identifies the account itself.");
        }

        if (remember && string.IsNullOrEmpty(username) && !useQr)
        {
            errors.Add("-remember-password needs -username (or -qr).");
        }

        if (password is { Length: > 64 })
        {
            warnings.Add("That password is longer than 64 characters; Steam will not accept it.");
        }

        if (password != null && password.Any(c => c > 127))
        {
            warnings.Add("That password contains non-ASCII characters, which Steam logins reject.");
        }

        uint? loginId = null;
        if (values.TryGetValue("loginid", out var loginIdText))
        {
            if (TryParseUInt(loginIdText, out var parsed))
            {
                loginId = parsed;
            }
            else
            {
                errors.Add($"-loginid expects a 32-bit number, got '{loginIdText}'.");
            }
        }
        else
        {
            loginId = defaults.LoginId;
        }

        var credentials = new SteamCredentials
        {
            Username = useQr ? null : username,
            PlainPassword = password,
            UseQrCode = useQr,
            RememberPassword = remember,
            PreferTwoFactorCode = flags.Contains("no-mobile") || defaults.PreferTwoFactorCode,
            LoginId = loginId,
        };

        var cellId = defaults.CellId;
        if (values.TryGetValue("cellid", out var cellIdText))
        {
            if (long.TryParse(cellIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCell))
            {
                cellId = parsedCell < 0 ? 0 : (uint)parsedCell;
            }
            else
            {
                errors.Add($"-cellid expects a number, got '{cellIdText}'.");
            }
        }

        var maxDownloadsGiven = values.TryGetValue("max-downloads", out var maxDownloadsText);
        var maxDownloads = defaults.MaxDownloads;

        if (maxDownloadsGiven)
        {
            if (int.TryParse(maxDownloadsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                maxDownloads = parsed;
            }
            else
            {
                errors.Add($"-max-downloads expects a positive number, got '{maxDownloadsText}'.");
            }
        }

        var useLancache = flags.Contains("use-lancache") || defaults.UseLancache;

        if (useLancache && !maxDownloadsGiven)
        {
            maxDownloads = Math.Max(maxDownloads, 25);
        }

        FileFilter? filter = null;
        var fileListPath = values.GetValueOrDefault("filelist") ?? defaults.FileList;

        if (fileListPath != null)
        {
            try
            {
                filter = FileFilter.FromFile(fileListPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or DepotDownloadException or ArgumentException)
            {
                errors.Add($"Could not read the file list '{fileListPath}': {ex.Message}");
            }
        }

        var downloadConfig = new DownloadConfig
        {
            InstallDirectory = values.GetValueOrDefault("dir") ?? defaults.InstallDirectory,
            CellId = cellId,
            MaxDownloads = maxDownloads,
            VerifyAll = flags.Contains("validate") || defaults.Validate,
            ManifestOnly = flags.Contains("manifest-only") || defaults.ManifestOnly,
            FileFilter = filter,
        };

        var debug = flags.Contains("debug") || defaults.Debug;

        var sessionOptions = new SteamSessionOptions
        {
            AccountStore = accountStore,
            CellIdOverride = cellId == 0 ? null : cellId,
            UseLancache = useLancache,
            Debug = debug,
        };

        var target = DownloadTargetKind.None;
        AppDownloadRequest? request = null;

        var appId = 0u;
        var hasApp = values.TryGetValue("app", out var appIdText);

        if (hasApp && !TryParseUInt(appIdText!, out appId))
        {
            errors.Add($"-app expects a number, got '{appIdText}'.");
            hasApp = false;
        }

        if (hasApp)
        {
            target = DownloadTargetKind.App;
        }

        if (manifestIds.Count > depotIds.Count)
        {
            errors.Add("Each -manifest must be paired with a -depot.");
        }

        if (target == DownloadTargetKind.App)
        {
            var selectors = depotIds
                .Select((id, index) => new DepotSelector(id,
                    index < manifestIds.Count ? manifestIds[index] : DepotConstants.InvalidManifestId))
                .ToList();

            var os = values.GetValueOrDefault("os") ?? defaults.Os;
            var arch = values.GetValueOrDefault("osarch") ?? defaults.Arch;

            if (os != null && !PlatformInfo.IsValidOs(os))
            {
                errors.Add($"-os expects windows, macos or linux, got '{os}'.");
            }

            if (arch != null && !PlatformInfo.IsValidArch(arch))
            {
                errors.Add($"-osarch expects 32 or 64, got '{arch}'.");
            }

            request = new AppDownloadRequest
            {
                AppId = appId,
                Depots = selectors,
                Branch = values.GetValueOrDefault("branch") ?? defaults.Branch,
                BranchPassword = values.GetValueOrDefault("branchpassword") ?? defaults.BranchPassword,
                Os = os,
                Arch = arch,
                Language = values.GetValueOrDefault("language") ?? defaults.Language,
                LowViolence = flags.Contains("lowviolence") || defaults.LowViolence,
                AllPlatforms = flags.Contains("all-platforms") || defaults.AllPlatforms,
                AllArchitectures = flags.Contains("all-archs") || defaults.AllArchitectures,
                AllLanguages = flags.Contains("all-languages") || defaults.AllLanguages,
            };
        }

        return new DepotArgs
        {
            Credentials = credentials,
            SessionOptions = sessionOptions,
            DownloadConfig = downloadConfig,
            Target = target,
            AppId = appId,
            Request = request,
            Debug = debug,
            ShowStatus = flags.Contains("status"),
            UnknownArguments = unknown,
            Errors = errors,
            Warnings = warnings,
        };
    }

    private static string Canonical(string name) => name.ToLowerInvariant() switch
    {
        "user" => "username",
        "pass" => "password",
        "beta" or "betabranch" => "branch",
        "betapassword" => "branchpassword",
        "verify-all" or "verify_all" => "validate",
        "v" => "version",
        "?" => "help",
        var other => other,
    };

    private static bool TryParseUInt(string text, out uint value)
        => uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static DepotArgs Minimal(DepotArgsDefaults defaults, bool showHelp = false, bool showVersion = false)
        => new()
        {
            Credentials = SteamCredentials.Anonymous,
            SessionOptions = new SteamSessionOptions(),
            DownloadConfig = new DownloadConfig { MaxDownloads = defaults.MaxDownloads },
            ShowHelp = showHelp,
            ShowVersion = showVersion,
        };
}
