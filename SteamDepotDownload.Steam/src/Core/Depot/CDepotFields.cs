using SteamDepotDownload.Steam.Shared.Depot;
using SteamKit2;

namespace SteamDepotDownload.Steam.Core.Depot;

internal static class CDepotFields
{
    internal static ulong ReadGid(KeyValue branchNode)
    {
        if (branchNode == KeyValue.Invalid)
        {
            return DepotConstants.InvalidManifestId;
        }

        var gid = branchNode["gid"];
        var value = gid == KeyValue.Invalid ? branchNode.AsUnsignedLong() : gid.AsUnsignedLong();

        return value == 0 ? DepotConstants.InvalidManifestId : value;
    }

    internal static ulong ReadSize(KeyValue branchNode, KeyValue depotNode)
    {
        if (branchNode != KeyValue.Invalid && branchNode["size"] != KeyValue.Invalid)
        {
            return branchNode["size"].AsUnsignedLong();
        }

        return depotNode == KeyValue.Invalid ? 0 : depotNode["maxsize"].AsUnsignedLong();
    }

    internal static ulong ReadDownloadSize(KeyValue branchNode)
        => branchNode == KeyValue.Invalid ? 0 : branchNode["download"].AsUnsignedLong();

    internal static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];

        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
