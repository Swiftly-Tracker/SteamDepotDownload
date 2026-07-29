using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Core.Session;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

/// <summary>
/// Turns "download app N" into a concrete list of depots, manifests and directories.
/// </summary>
internal sealed class CDepotResolver
{
    private readonly CSteamSession _session;
    private readonly DownloadConfig _config;

    internal CDepotResolver(CSteamSession session, DownloadConfig config)
    {
        _session = session;
        _config = config;
    }

    internal async Task<IReadOnlyList<DepotInfo>> DescribeAsync(AppDownloadRequest request, CancellationToken ct)
    {
        var depots = await GetDepotSectionAsync(request, ct).ConfigureAwait(false);
        if (depots == null)
        {
            return [];
        }

        var os = request.Os ?? PlatformInfo.HostOs;
        var arch = request.Arch ?? PlatformInfo.HostArch;

        var results = new List<DepotInfo>();

        foreach (var depotId in SelectDepotIds(request, depots, os, arch, _config.FileFilter))
        {
            var node = depots[depotId.ToString()];
            var config = node["config"];

            var branchNode = await ResolveBranchNodeAsync(depotId, request.AppId, request.Branch, depots, ct)
                .ConfigureAwait(false);

            var selected = request.Depots.FirstOrDefault(d => d.DepotId == depotId);
            var manifestId = selected.HasManifest ? selected.ManifestId : CDepotFields.ReadGid(branchNode);

            results.Add(new DepotInfo
            {
                DepotId = depotId,
                AppId = node["depotfromapp"] != KeyValue.Invalid
                    ? node["depotfromapp"].AsUnsignedInteger()
                    : request.AppId,
                Name = node["name"].AsString(),
                ManifestId = manifestId,
                Branch = request.Branch,
                SizeOnDisk = CDepotFields.ReadSize(branchNode, node),
                DownloadSize = CDepotFields.ReadDownloadSize(branchNode),
                Os = config["oslist"].AsString(),
                Arch = config["osarch"].AsString(),
                Language = config["language"].AsString(),
                LowViolence = config["lowviolence"].AsBoolean(),
                SharedInstall = node["sharedinstall"].AsBoolean(),
            });
        }

        return results;
    }

    internal async Task<List<CResolvedDepot>> ResolveAsync(AppDownloadRequest request, CancellationToken ct)
    {
        var depots = await GetDepotSectionAsync(request, ct).ConfigureAwait(false)
            ?? throw new DepotDownloadException($"App {request.AppId} publishes no depots.");

        var buildId = await GetBuildIdAsync(request.AppId, request.Branch, ct).ConfigureAwait(false);

        var os = request.Os ?? PlatformInfo.HostOs;
        var arch = request.Arch ?? PlatformInfo.HostArch;

        var resolved = new List<CResolvedDepot>();

        foreach (var depotId in SelectDepotIds(request, depots, os, arch, _config.FileFilter))
        {
            if (_config.FileFilter is { IsPerDepot: true } filter && !filter.Covers(depotId))
            {
                CSteamLog.Msg(CSteamLog.Depot,
                    $"Skipping depot {depotId}: the file list has no rules for it.");
                continue;
            }

            var node = depots[depotId.ToString()];

            var owningApp = node["depotfromapp"] != KeyValue.Invalid
                ? node["depotfromapp"].AsUnsignedInteger()
                : request.AppId;

            if (!await _session.HasAccessAsync(request.AppId, depotId, ct).ConfigureAwait(false))
            {
                if (!await _session.RequestFreeLicenseAsync(request.AppId, ct).ConfigureAwait(false) ||
                    !await _session.HasAccessAsync(request.AppId, depotId, ct).ConfigureAwait(false))
                {
                    CSteamLog.Warning(CSteamLog.Depot,
                        $"Skipping depot {depotId}: this account does not have access to it.");
                    continue;
                }
            }

            var selector = request.Depots.FirstOrDefault(d => d.DepotId == depotId);

            var manifestId = selector.HasManifest
                ? selector.ManifestId
                : await ResolveManifestIdAsync(depotId, request.AppId, request.Branch, depots, ct).ConfigureAwait(false);

            if (manifestId is DepotConstants.InvalidManifestId or 0)
            {
                CSteamLog.Warning(CSteamLog.Depot,
                    $"Skipping depot {depotId}: no manifest published for branch '{request.Branch}'.");
                continue;
            }

            var key = await _session.GetDepotKeyAsync(depotId, owningApp, ct).ConfigureAwait(false);
            if (key == null)
            {
                CSteamLog.Warning(CSteamLog.Depot,
                    $"Skipping depot {depotId}: no decryption key available.");
                continue;
            }

            resolved.Add(new CResolvedDepot
            {
                DepotId = depotId,
                AppId = owningApp,
                ManifestId = manifestId,
                Branch = request.Branch,
                InstallDirectory = CreateDirectories(depotId, buildId),
                DepotKey = key,
                BuildId = buildId,
                Name = node["name"].AsString(),
            });
        }

        if (resolved.Count == 0)
        {
            throw new DepotDownloadException(
                $"Nothing to download for app {request.AppId} on branch '{request.Branch}'. " +
                "Check the depot filters, the branch name and the account's licenses.");
        }

        return resolved;
    }

    internal async Task<uint> GetBuildIdAsync(uint appId, string branch, CancellationToken ct)
    {
        var info = await _session.GetProductInfoAsync(appId, ct).ConfigureAwait(false);
        if (info == null)
        {
            return 0;
        }

        var node = info["depots"]["branches"][branch]["buildid"];
        return node == KeyValue.Invalid ? 0 : node.AsUnsignedInteger();
    }

    internal string CreateDirectories(uint depotId, uint buildId)
    {
        var installDirectory = string.IsNullOrEmpty(_config.InstallDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), DepotConstants.DefaultDownloadDirectory,
                depotId.ToString(), buildId.ToString())
            : Path.GetFullPath(_config.InstallDirectory);

        var configDirectory = Path.Combine(installDirectory, DepotConstants.ConfigDirectory);
        var stagingDirectory = Path.Combine(configDirectory, DepotConstants.StagingDirectory);

        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(Path.Combine(configDirectory, DepotConstants.ManifestDirectory));
        Directory.CreateDirectory(stagingDirectory);

        SweepStaging(stagingDirectory);

        return installDirectory;
    }

    /// <remarks>
    /// A crash midway through a delta rewrite leaves the original file parked in staging and a
    /// half-written one at its real path. Nothing reads staging across runs, so anything still
    /// here is debris from a previous run that would otherwise never be reclaimed.
    /// </remarks>
    private static void SweepStaging(string stagingDirectory)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories))
            {
                File.Delete(path);
                CSteamLog.Detailed(CSteamLog.Depot, $"Discarded the abandoned staging file {path}.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CSteamLog.Warning(CSteamLog.Depot, $"Could not clear {stagingDirectory}: {ex.Message}");
        }
    }

    private async Task<KeyValue?> GetDepotSectionAsync(AppDownloadRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.BranchPassword))
        {
            return await _session
                .GetPrivateBranchDepotsAsync(request.AppId, request.Branch, request.BranchPassword, ct)
                .ConfigureAwait(false);
        }

        var info = await _session.GetProductInfoAsync(request.AppId, ct).ConfigureAwait(false);
        if (info == null)
        {
            return null;
        }

        var depots = info["depots"];
        return depots == KeyValue.Invalid ? null : depots;
    }

    private static IEnumerable<uint> SelectDepotIds(AppDownloadRequest request, KeyValue depots,
        string os, string arch, FileFilter? filter)
    {
        if (request.Depots.Count > 0)
        {
            return request.Depots.Select(depot => depot.DepotId).Distinct();
        }

        if (filter is { IsPerDepot: true, IsEmpty: false })
        {
            return filter.DepotIds;
        }

        var selected = new List<uint>();

        foreach (var node in depots.Children)
        {
            if (!uint.TryParse(node.Name, out var depotId))
            {
                continue;
            }

            if (node["manifests"] == KeyValue.Invalid && node["depotfromapp"] == KeyValue.Invalid)
            {
                continue;
            }

            if (!Matches(node["config"], request, os, arch))
            {
                continue;
            }

            selected.Add(depotId);
        }

        return selected;
    }

    private static bool Matches(KeyValue config, AppDownloadRequest request, string os, string arch)
    {
        if (config == KeyValue.Invalid)
        {
            return true;
        }

        if (!request.LowViolence && config["lowviolence"].AsBoolean())
        {
            return false;
        }

        if (!request.AllPlatforms && config["oslist"].AsString() is { Length: > 0 } oslist)
        {
            if (!oslist.Split(',').Any(entry => entry.Trim().Equals(os, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (!request.AllArchitectures && config["osarch"].AsString() is { Length: > 0 } osarch)
        {
            if (!osarch.Equals(arch, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!request.AllLanguages && config["language"].AsString() is { Length: > 0 } language)
        {
            if (!language.Equals(request.Language, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<ulong> ResolveManifestIdAsync(uint depotId, uint appId, string branch, KeyValue depots,
        CancellationToken ct)
        => CDepotFields.ReadGid(await ResolveBranchNodeAsync(depotId, appId, branch, depots, ct)
            .ConfigureAwait(false));

    private async Task<KeyValue> ResolveBranchNodeAsync(uint depotId, uint appId, string branch, KeyValue depots,
        CancellationToken ct)
    {
        var node = depots[depotId.ToString()];
        if (node == KeyValue.Invalid)
        {
            return KeyValue.Invalid;
        }

        var manifests = node["manifests"];

        if (manifests == KeyValue.Invalid && node["depotfromapp"] != KeyValue.Invalid)
        {
            var otherAppId = node["depotfromapp"].AsUnsignedInteger();

            if (otherAppId != appId && otherAppId != 0)
            {
                var otherInfo = await _session.GetProductInfoAsync(otherAppId, ct).ConfigureAwait(false);
                var otherDepots = otherInfo?["depots"];

                if (otherDepots != null && otherDepots != KeyValue.Invalid)
                {
                    manifests = otherDepots[depotId.ToString()]["manifests"];
                }
            }
        }

        if (manifests == KeyValue.Invalid)
        {
            return KeyValue.Invalid;
        }

        var branchNode = manifests[branch];

        if (CDepotFields.ReadGid(branchNode) == DepotConstants.InvalidManifestId &&
            !branch.Equals(DepotConstants.PublicBranch, StringComparison.OrdinalIgnoreCase))
        {
            CSteamLog.Warning(CSteamLog.Depot,
                $"Depot {depotId} has no content on branch '{branch}'; falling back to '{DepotConstants.PublicBranch}'.");

            branchNode = manifests[DepotConstants.PublicBranch];
        }

        return branchNode;
    }
}
