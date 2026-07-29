namespace SteamDepotDownload.Tier0.Shared.Terminal;

public static class CommandRegistry
{
    private static readonly Lock _lock = new();
    private static readonly List<IConCommand> _pending = [];
    private static ICommandSink? _sink;

    public static void Register(IConCommand command)
    {
        lock (_lock)
        {
            if (_sink != null)
            {
                _sink.Index(command);
                return;
            }

            _pending.Add(command);
        }
    }

    public static void Attach(ICommandSink sink)
    {
        lock (_lock)
        {
            _sink = sink;

            foreach (var command in _pending)
            {
                sink.Index(command);
            }

            _pending.Clear();
        }
    }
}
