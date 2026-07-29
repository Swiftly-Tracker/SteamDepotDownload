namespace SteamDepotDownload.Tier0.Shared.ConVar;

public interface IConVarSystem
{
    IConVar? Find(string name);
    bool Exists(string name);
    int Count { get; }
    IEnumerable<IConVar> GetAll();
    event Action<IConVar>? ConVarChanged;
}
