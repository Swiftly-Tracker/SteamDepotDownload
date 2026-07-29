using SteamDepotDownload.Tier0.Shared.ConVar;
using SteamDepotDownload.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Tier0.Core.ConVar;

[ExposeInterface(InterfaceNames.ConVar)]
internal sealed class CConVarSystem : IConVarSystem, IConVarSink
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, IConVar> _convars = new(StringComparer.OrdinalIgnoreCase);

    public event Action<IConVar>? ConVarChanged;

    public CConVarSystem()
    {
        ConVarRegistry.Attach(this);
    }

    public void Index(IConVar convar)
    {
        lock (_lock)
        {
            if (!_convars.TryAdd(convar.Name, convar))
            {
                throw new InvalidOperationException($"A ConVar named '{convar.Name}' is already registered.");
            }
        }
    }

    public void RaiseChanged(IConVar convar)
    {
        ConVarChanged?.Invoke(convar);
    }

    public IConVar? Find(string name)
    {
        lock (_lock)
        {
            return _convars.GetValueOrDefault(name);
        }
    }

    public bool Exists(string name)
    {
        lock (_lock)
        {
            return _convars.ContainsKey(name);
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _convars.Count;
            }
        }
    }

    public IEnumerable<IConVar> GetAll()
    {
        lock (_lock)
        {
            return [.. _convars.Values];
        }
    }
}
