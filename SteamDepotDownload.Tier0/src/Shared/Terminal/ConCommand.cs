namespace SteamDepotDownload.Tier0.Shared.Terminal;

public sealed class ConCommand : IConCommand
{
    private readonly Action<CommandContext> _callback;

    public string Name { get; }
    public string? Description { get; }

    public ConCommand(string name, Action<CommandContext> callback, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("ConCommand name must not be empty.", nameof(name));
        }

        Name = name;
        Description = description;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));

        CommandRegistry.Register(this);
    }

    public void Invoke(CommandContext context) => _callback(context);
}
