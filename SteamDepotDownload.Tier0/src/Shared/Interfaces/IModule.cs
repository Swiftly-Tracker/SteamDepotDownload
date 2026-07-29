namespace SteamDepotDownload.Tier0.Shared.Interfaces;

public interface IModule
{
    void Init(IInterfaceSystem system);

    void Shutdown();
}
