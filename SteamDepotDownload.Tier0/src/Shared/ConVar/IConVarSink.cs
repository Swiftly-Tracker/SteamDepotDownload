namespace SteamDepotDownload.Tier0.Shared.ConVar;

public interface IConVarSink
{
    void Index(IConVar convar);
    void RaiseChanged(IConVar convar);
}
