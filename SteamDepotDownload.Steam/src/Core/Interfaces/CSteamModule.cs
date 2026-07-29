using SteamDepotDownload.Steam.Core.Depot;
using SteamDepotDownload.Steam.Shared.Interfaces;
using SteamDepotDownload.Steam.Shared.Jobs;
using SteamDepotDownload.Tier0.Shared.Drawing;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Logging;

namespace SteamDepotDownload.Steam.Core.Interfaces;

internal sealed class CSteamModule : IModule
{
    internal const string SteamChannel = "Steam";
    internal const string DepotChannel = "Depot";
    internal const string CdnChannel = "CDN";

    public void Init(IInterfaceSystem system)
    {
        var logging = system.GetInterface<ILoggingSystem>(InterfaceNames.LoggingSystem);

        logging?.RegisterChannel(SteamChannel, color: new Color(102, 192, 244));
        logging?.RegisterChannel(DepotChannel, color: new Color(166, 208, 87));
        logging?.RegisterChannel(CdnChannel, LoggingChannelFlags.None, LoggingVerbosity.Essential,
            new Color(200, 160, 80));

        system.GetInterface<IDownloadJobs>(SteamInterfaceNames.DownloadJobs);

        CDepotConVars.Register();
        CDepotCommands.Register();
    }

    public void Shutdown()
    {
        var jobs = InterfaceSystem.GetInterface<IDownloadJobs>(SteamInterfaceNames.DownloadJobs);
        jobs?.CancelAll();

        Session.CSessionHolder.LogoutAsync().GetAwaiter().GetResult();
    }
}
