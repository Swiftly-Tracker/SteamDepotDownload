namespace SteamDepotDownload.Tier0.Shared.Terminal;

public interface ICommandSink
{
    void Index(IConCommand command);
}
