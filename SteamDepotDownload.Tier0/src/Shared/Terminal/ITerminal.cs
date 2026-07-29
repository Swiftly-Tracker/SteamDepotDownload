namespace SteamDepotDownload.Tier0.Shared.Terminal;

public interface ITerminal
{
    void Execute(string line);
    void Run();
    void Stop();
    bool IsRunning { get; }
    IConCommand? FindCommand(string name);
    IEnumerable<IConCommand> GetCommands();
}
