using System.Reflection;
using SteamDepotDownload.Tier0.Shared.Interfaces;

namespace SteamDepotDownload.Tier0.Core.Interfaces;

internal sealed class CInterfaceSystem : IInterfaceSystem
{
    private sealed class Registration
    {
        public required Type ImplType;
        public required bool Singleton;
        public object? Instance;
    }

    private readonly Dictionary<string, Registration> _registrations = [];
    private readonly List<IModule> _modules = [];

    public void AddModule(Assembly assembly)
    {
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<ExposeInterfaceAttribute>(inherit: false);
            if (attr is null)
            {
                continue;
            }

            if (_registrations.ContainsKey(attr.Name))
            {
                throw new InvalidOperationException(
                    $"Interface '{attr.Name}' is already registered (by " +
                    $"'{_registrations[attr.Name].ImplType.FullName}'); '{type.FullName}' conflicts.");
            }

            _registrations[attr.Name] = new Registration
            {
                ImplType = type,
                Singleton = attr.Singleton,
            };
        }

        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(type))
            {
                var module = (IModule)Activator.CreateInstance(type)!;
                module.Init(this);
                _modules.Add(module);
            }
        }
    }

    public T? GetInterface<T>(string name) where T : class
    {
        return GetInterface(name) as T;
    }

    public object? GetInterface(string name)
    {
        if (!_registrations.TryGetValue(name, out var reg))
        {
            return null;
        }

        if (!reg.Singleton)
        {
            return Activator.CreateInstance(reg.ImplType);
        }

        return reg.Instance ??= Activator.CreateInstance(reg.ImplType);
    }

    public bool HasInterface(string name)
    {
        return _registrations.ContainsKey(name);
    }

    public void RemoveAll()
    {
        for (int i = _modules.Count - 1; i >= 0; i--)
        {
            _modules[i].Shutdown();
        }

        _modules.Clear();
        _registrations.Clear();
    }
}
