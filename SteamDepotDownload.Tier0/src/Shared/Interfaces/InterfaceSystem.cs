using System.Reflection;
using SteamDepotDownload.Tier0.Core.Interfaces;

namespace SteamDepotDownload.Tier0.Shared.Interfaces;

public static class InterfaceSystem
{
    private static readonly CInterfaceSystem _system = new();

    public static IInterfaceSystem Instance => _system;

    public static void LoadModule(string assemblyName)
    {
        AddModule(Assembly.Load(assemblyName));
    }

    public static void AddModule(Assembly assembly) => _system.AddModule(assembly);

    public static T? GetInterface<T>(string name) where T : class => _system.GetInterface<T>(name);

    public static object? GetInterface(string name) => _system.GetInterface(name);

    public static bool HasInterface(string name) => _system.HasInterface(name);

    public static void RemoveAll() => _system.RemoveAll();
}
