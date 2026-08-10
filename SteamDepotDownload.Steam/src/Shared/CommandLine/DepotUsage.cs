using System.Reflection;

namespace SteamDepotDownload.Steam.Shared.CommandLine;

public static class DepotUsage
{
    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static string VersionLine =>
        $"SteamDepotDownload {Version} on {Environment.OSVersion} / .NET {Environment.Version}";

    public const string Text = """
        Usage: SteamDepotDownload.App [options]

        Run with no download target to open the interactive terminal.

        Authentication
          -username <user>       account to log in as (alias: -user)
          -password <pass>       password; omit to be prompted (alias: -pass)
          -remember-password     remember this login for later runs
          -qr                    log in by scanning a QR code with the Steam mobile app
          -no-mobile             type a Steam Guard code instead of approving in the app
          -loginid <#>           unique 32-bit id, needed to run several instances at once

        Downloading
          -app <#>               app to download
          -pubfile <id>          Workshop item to download (looks up its owning app itself)
          -ugc <id>              raw UGC content id to download; needs -app
          -depot <#>             depot to download; repeat for several
          -manifest <id>         manifest for the preceding -depot
          -branch <name>         branch to download from (default: public; alias: -beta)
          -branchpassword <pass> password for a private branch (alias: -betapassword)

        Download configuration
          -dir <path>            where to put the files (default: depots/<depot>/<build>)
          -filelist <file>       only download the listed files; prefix a line with regex:,
                                 or pass a JSON object of depot id to a list of rules
          -validate              checksum files that are already present (alias: -verify-all)
          -manifest-only         write a readable manifest instead of downloading
          -cellid <#>            override the content-server cell
          -max-downloads <#>     chunks to fetch at once (default: 32)
          -use-lancache          route downloads through a Lancache on this network
          -os <os>               windows, macos or linux (default: this host)
          -osarch <arch>         32 or 64 (default: this host)
          -language <lang>       language depots to take (default: english)
          -all-platforms         take every platform's depots
          -all-archs             take every architecture's depots
          -all-languages         take every language's depots
          -lowviolence           include low-violence depots

        Other
          -status                show what is installed on disk and exit; reads local
                                 state only, never contacts Steam. Honours -dir, and
                                 -app narrows it to one app
          -debug                 verbose logging
          -V, --version          print the version
          -help                  print this
        """;
}
