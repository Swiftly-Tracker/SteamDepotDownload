namespace SteamDepotDownload.Tier0.Shared.ConVar;

public interface IConVar
{
    string Name { get; }
    string? Description { get; }
    ConVarFlags Flags { get; }
    Type ValueType { get; }
    bool HasBounds { get; }
    string ToStringValue();
    void SetFromString(string value);
    void SetFromString(string value, bool force);
}
