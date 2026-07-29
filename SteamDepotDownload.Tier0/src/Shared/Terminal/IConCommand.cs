namespace SteamDepotDownload.Tier0.Shared.Terminal;

public interface IConCommand
{
    string Name { get; }
    string? Description { get; }
    void Invoke(CommandContext context);
}
