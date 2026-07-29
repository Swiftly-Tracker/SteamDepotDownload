namespace SteamDepotDownload.Tier0.Shared.ConVar;

public static class ConVarRegistry
{
    private static readonly Lock _lock = new();
    private static readonly List<IConVar> _pending = [];
    private static IConVarSink? _sink;

    public static void Register(IConVar convar)
    {
        lock (_lock)
        {
            if (_sink != null)
            {
                _sink.Index(convar);
                return;
            }

            _pending.Add(convar);
        }
    }

    public static void Attach(IConVarSink sink)
    {
        lock (_lock)
        {
            _sink = sink;

            foreach (var convar in _pending)
            {
                sink.Index(convar);
            }

            _pending.Clear();
        }
    }

    public static void NotifyChanged(IConVar convar)
    {
        IConVarSink? sink;
        lock (_lock)
        {
            sink = _sink;
        }

        sink?.RaiseChanged(convar);
    }
}
