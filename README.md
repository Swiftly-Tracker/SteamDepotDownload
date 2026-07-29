# SteamDepotDownload

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![Build Status](https://img.shields.io/github/actions/workflow/status/Swiftly-Tracker/SteamDepotDownload/build.yml?branch=main)](https://github.com/Swiftly-Tracker/SteamDepotDownload/actions)
[![Release](https://img.shields.io/github/v/release/Swiftly-Tracker/SteamDepotDownload?include_prereleases)](https://github.com/Swiftly-Tracker/SteamDepotDownload/releases)
[![NuGet](https://img.shields.io/nuget/v/SteamDepotDownload.svg)](https://www.nuget.org/packages/SteamDepotDownload/)

Download Steam game content without needing the Steam client. Works as both a CLI tool and a .NET library.

Uses [SteamKit2](https://github.com/SteamRE/SteamKit) to pull directly from Steam's CDN. Download whole apps, specific depots, pinned manifests, or alternate branches.

## Install

Grab an archive from the [latest release](https://github.com/Swiftly-Tracker/SteamDepotDownload/releases/latest):

| Archive | Needs .NET installed? | Use when |
| --- | --- | --- |
| [`SteamDepotDownload-win-x64.zip`](https://github.com/Swiftly-Tracker/SteamDepotDownload/releases/latest/download/SteamDepotDownload-win-x64.zip) | No | Windows, just run it |
| [`SteamDepotDownload-linux-x64.zip`](https://github.com/Swiftly-Tracker/SteamDepotDownload/releases/latest/download/SteamDepotDownload-linux-x64.zip) | No | Linux, just run it |
| [`SteamDepotDownload-win-x64-portable.zip`](https://github.com/Swiftly-Tracker/SteamDepotDownload/releases/latest/download/SteamDepotDownload-win-x64-portable.zip) | .NET 10 runtime | Windows, smaller download |
| [`SteamDepotDownload-linux-x64-portable.zip`](https://github.com/Swiftly-Tracker/SteamDepotDownload/releases/latest/download/SteamDepotDownload-linux-x64-portable.zip) | .NET 10 runtime | Linux, smaller download |

Those links always resolve to the newest stable release. On Linux, `chmod +x SteamDepotDownload.App` after unzipping.

As a library:

```bash
dotnet add package SteamDepotDownload
```

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

The `SteamDepotDownload` NuGet package has no terminal or CLI dependencies. Use it headless in your own app.

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

## Building from source

Requires the **.NET 10 SDK**.

```bash
git clone https://github.com/Swiftly-Tracker/SteamDepotDownload.git
cd SteamDepotDownload
dotnet build SteamDepotDownload.slnx -c Release
```

Output lands in `build/Release/<project>/`; the CLI is `build/Release/SteamDepotDownload.App/SteamDepotDownload.App`.

To produce a standalone binary like the release archives:

```bash
dotnet publish SteamDepotDownload.App/SteamDepotDownload.App.csproj -c Release \
  -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=false \
  -o out/linux-x64
```

> `PublishTrimmed` must stay `false`. The app resolves its modules by name through `Assembly.Load`, which the trimmer cannot see.

## Releases & branches

| Branch | Publishes | Tag |
| --- | --- | --- |
| `main` | Stable release | `vX.Y.Z` |
| `beta` | Prerelease | `vX.Y.Z-beta.N` |

The flow:

1. Features and fixes land on **`beta`**. Every push builds and publishes a prerelease with all four archives attached.
2. When a batch is ready, open a PR from **`beta` → `main`**. Merging it publishes the stable release and pushes the NuGet package.
3. `beta` is then automatically force-reset onto `main`, so the next cycle starts clean. If you keep a local `beta`, run `git fetch && git reset --hard origin/beta` after each stable release.

## Architecture

```
SteamDepotDownload/
├── SteamDepotDownload.Tier0/        # Framework layer
│   └── src/
│       ├── Core/                    # ConVar system, logging, terminal, serialization
│       └── Shared/                  # Public interfaces (IInterfaceSystem, IConVar, ITerminal, ...)
├── SteamDepotDownload.Steam/        # Downloader
│   └── src/
│       ├── Core/                    # Session, CDN pool, chunk pump, manifest cache, state store
│       └── Shared/                  # Public API (ISteamSession, IDepotFetcher, DownloadConfig, ...)
├── SteamDepotDownload.App/          # CLI entry point
│   └── src/Application.cs
├── SteamDepotDownload.csproj        # NuGet packaging front
├── Directory.Build.props            # Shared metadata + version
└── GitVersion.yml                   # Versioning rules
```

## Community

- **Issues**: [Report bugs and request features](https://github.com/Swiftly-Tracker/SteamDepotDownload/issues)
- **Security**: [Report privately](https://github.com/Swiftly-Tracker/SteamDepotDownload/security/advisories/new) — never in a public issue

## License

GPL-3.0. See [LICENSE](LICENSE). Third-party attributions in [THIRDPARTY.md](THIRDPARTY.md).

---

<div align="center">
  <strong>Made with ❤️ by the Swiftly Development team</strong>
</div>
