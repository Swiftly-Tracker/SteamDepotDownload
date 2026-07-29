using SteamDepotDownload.Steam.Core.Depot;

namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed record DepotInstallSite
{
    public required string Directory { get; init; }

    public required IReadOnlyList<DepotInstallRecord> Depots { get; init; }
}

/// <summary>
/// Reads what a previous download recorded on disk. Never contacts Steam.
/// </summary>
public static class DepotState
{
    public static DepotInstallSite? Read(string installDirectory)
    {
        var full = Path.GetFullPath(installDirectory);

        if (!File.Exists(CDepotStateStore.PathFor(full)))
        {
            return null;
        }

        var depots = new CDepotStateStore(CDepotStateStore.PathFor(full)).Records;

        return depots.Count == 0
            ? null
            : new DepotInstallSite { Directory = full, Depots = depots };
    }

    public static IReadOnlyList<DepotInstallSite> Discover(string? installDirectory)
    {
        if (!string.IsNullOrWhiteSpace(installDirectory))
        {
            var site = Read(installDirectory);
            return site == null ? [] : [site];
        }

        var root = Path.Combine(System.IO.Directory.GetCurrentDirectory(),
            DepotConstants.DefaultDownloadDirectory);

        if (!System.IO.Directory.Exists(root))
        {
            return [];
        }

        var sites = new List<DepotInstallSite>();

        try
        {
            foreach (var depotDirectory in System.IO.Directory.EnumerateDirectories(root))
            {
                foreach (var buildDirectory in System.IO.Directory.EnumerateDirectories(depotDirectory))
                {
                    if (Read(buildDirectory) is { } site)
                    {
                        sites.Add(site);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return sites;
        }

        return sites;
    }

    public static string ScanRoot => Path.Combine(System.IO.Directory.GetCurrentDirectory(),
        DepotConstants.DefaultDownloadDirectory);

    public static IEnumerable<string> Describe(IReadOnlyList<DepotInstallSite> sites, uint? appId = null)
    {
        var entries = sites
            .SelectMany(site => site.Depots.Select(depot => (site.Directory, Depot: depot)))
            .Where(entry => appId == null || entry.Depot.AppId == appId)
            .GroupBy(entry => entry.Depot.DepotId)
            .OrderBy(group => group.Key);

        var depotCount = 0;
        var installCount = 0;

        foreach (var group in entries)
        {
            var installs = group.OrderByDescending(entry => entry.Depot.BuildId).ToList();
            var name = installs[0].Depot.Name;

            depotCount++;
            installCount += installs.Count;

            yield return $"depot {group.Key}{(name == null ? string.Empty : $"  {name}")}";

            for (var i = 0; i < installs.Count; i++)
            {
                var depot = installs[i].Depot;

                var detail = $"  manifest {depot.ManifestId}  build {depot.BuildId}  " +
                    $"branch {depot.Branch}  app {depot.AppId}";

                // "newest build", never "current": nothing on disk records which one is in use.
                if (i == 0 && installs.Count > 1)
                {
                    detail += "  <- newest build";
                }

                yield return detail;
                yield return $"    {CDepotFields.FormatBytes(depot.SizeOnDisk)}, {depot.FileCount:N0} files, " +
                    $"updated {depot.UpdatedUtc.UtcDateTime:yyyy-MM-dd HH:mm}Z";
                yield return $"    {installs[i].Directory}";
            }
        }

        if (depotCount > 0)
        {
            yield return $"{depotCount} depot{(depotCount == 1 ? string.Empty : "s")}, " +
                $"{installCount} install{(installCount == 1 ? string.Empty : "s")}";
        }
    }
}
