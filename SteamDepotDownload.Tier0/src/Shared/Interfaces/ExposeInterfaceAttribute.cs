namespace SteamDepotDownload.Tier0.Shared.Interfaces;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ExposeInterfaceAttribute : Attribute
{
    public string Name { get; }

    public bool Singleton { get; init; } = true;

    public ExposeInterfaceAttribute(string name)
    {
        Name = name;
    }
}
