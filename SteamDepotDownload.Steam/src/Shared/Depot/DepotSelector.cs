namespace SteamDepotDownload.Steam.Shared.Depot;

public readonly record struct DepotSelector(uint DepotId, ulong ManifestId = DepotConstants.InvalidManifestId)
{
    public bool HasManifest => ManifestId is not (DepotConstants.InvalidManifestId or 0);
}
