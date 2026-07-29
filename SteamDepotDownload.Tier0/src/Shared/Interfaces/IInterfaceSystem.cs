namespace SteamDepotDownload.Tier0.Shared.Interfaces;

public interface IInterfaceSystem
{
    T? GetInterface<T>(string name) where T : class;

    object? GetInterface(string name);

    bool HasInterface(string name);
}
