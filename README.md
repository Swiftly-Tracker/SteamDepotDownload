# SteamDepotDownload

Download Steam game content without needing the Steam client. Works as both a CLI tool and a .NET library.

Uses [SteamKit2](https://github.com/SteamRE/SteamKit) to pull directly from Steam's CDN. Download whole apps, specific depots, pinned manifests, or alternate branches.

## Layout

| Project | Purpose |
| --- | --- |
| `SteamDepotDownload.Tier0` | Core framework: interface registry, ConVars, ConCommands, logging, terminal REPL. |
| `SteamDepotDownload.Steam` | Downloader library. Public API in `src/Shared/`, implementation in `src/Core/`. |
| `SteamDepotDownload.App` | CLI entry point. One-shot download when you pass a target, or interactive terminal. |

## Quick start

Download Steamworks Redist (app 1007) to a custom directory:
```bash
./SteamDepotDownload.App -app 1007 -depot 1004 -dir ./redist
```

Just dump the manifest without downloading:
```bash
./SteamDepotDownload.App -app 1007 -manifest-only
```

Verify existing files and re-download changed chunks:
```bash
./SteamDepotDownload.App -username alice -app 440 -depot 441 -validate
```

See what a previous download left on disk, without touching the network:
```bash
./SteamDepotDownload.App -status -dir ./redist
```

Show all available flags:
```bash
./SteamDepotDownload.App -help
```

**Smart re-runs:** Comparing the manifest first makes re-running almost instant — only changed chunks re-fetch. Use `-validate` to checksum existing files instead of trusting the manifest.

## Where files go

Without `-dir`, downloads land in `depots/{depotId}/{buildId}`. Bookkeeping lives in a `.sdd/` folder beside the game files:

```
redist/
  .sdd/
    state.sdb                             what is installed, and which manifest
    manifests/{depotId}-{manifestId}.sdm  cached manifests
    dumps/                                -manifest-only output
    staging/                              scratch space for patching
  <game files>
```

`depot_status` (or `-status`) prints the state file's contents without contacting Steam, and `-manifest-only` writes a plain-text listing of any manifest.

## Filter which files to download

Use `-filelist <file>` (or the `depot_filelist` ConVar in the terminal) to download only specific files.

**Format 1: Plain list** (one rule per line, applies to all depots)
```
bin/win64/client.dll
regex:.+?_dir\.vpk
```

**Format 2: JSON per-depot** (pick depots and rules separately)
```json
{
    "2347770": ["regex:.+?\\.(inf|dll)"],
    "2347771": ["regex:.+?\\.(dll|exe)"],
    "2347773": ["regex:.+?\\.(sh|so)"]
}
```

The JSON format also **selects which depots to download**. Naming a depot in the map is like passing `-depot` — you get it even if it doesn't match your platform. So the example pulls the Linux depot (2347773) even on Windows. Omit a depot and it won't download.

Use literal paths for exact files (matched case-insensitively), `regex:` for patterns. Literals are full manifest paths; regex matches anywhere.

## Interactive terminal

Run with no target to get a REPL:
```bash
./SteamDepotDownload.App
```

Try commands:
```
steam_login_anonymous
depot_filelist ./depots.json
download_app 730
download_status
download_cancel
depot_status
```

Every CLI flag has a matching `depot_*` ConVar (run `convars` to list). So you can also:
```bash
./SteamDepotDownload.App -depot_max_downloads 16 +download_app 1007
```

2FA or email code prompts get asked via `steam_code <value>` so the terminal can stay responsive. Run `help <command>` for details.

## Use as a library

The `SteamDepotDownload.Steam` NuGet package has no terminal or CLI dependencies. Use it headless in your own app.

```csharp
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Steam.Shared.Session;

await using var session = await SteamClientFactory.Create()
    .ConnectAsync(SteamCredentials.Anonymous, ct: token);

var downloader = session.CreateDownloader(new DownloadConfig { InstallDirectory = "out" });

var result = await downloader.DownloadAppAsync(
    new AppDownloadRequest { AppId = 1007 },
    new Progress<DownloadProgress>(p => Console.WriteLine($"{p.Fraction:P0} {p.CurrentFile}")),
    token);
```

No console output unless you wire up an `ISteamAuthenticator` (for logins). Multiple sessions can run concurrently in one process.

## Stored credentials

`-remember-password` saves refresh tokens to `%LOCALAPPDATA%/SteamDepotDownload/account.config` (or `$XDG_DATA_HOME/SteamDepotDownload` on Unix, owner-only). It is written in a binary format, which obscures nothing — the tokens sit in plain UTF-8 inside it. This file is a credential; treat it like a password. Anyone who reads it can log in until the token expires.

On shared machines, use `steam_logout` instead. To use a different path, pass `AccountSettingsStore.CreateAt(path)` when building the library session.

## License

GPL-3.0. See [LICENSE](LICENSE).
