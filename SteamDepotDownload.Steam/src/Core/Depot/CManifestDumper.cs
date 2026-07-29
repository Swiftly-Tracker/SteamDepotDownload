using System.Text;

namespace SteamDepotDownload.Steam.Core.Depot;

internal static class CManifestDumper
{
    internal static string Write(string directory, CManifestData manifest)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"manifest_{manifest.DepotId}_{manifest.ManifestGid}.txt");
        var text = new StringBuilder();

        text.AppendLine($"Content Manifest for Depot {manifest.DepotId}");
        text.AppendLine();
        text.AppendLine($"Manifest ID / date     : {manifest.ManifestGid} / {manifest.CreationTime:O}");
        text.AppendLine($"Total number of files  : {manifest.Entries.Count}");
        text.AppendLine($"Total number of chunks : {manifest.Entries.Sum(file => file.Chunks.Count)}");
        text.AppendLine($"Total bytes on disk    : {manifest.TotalUncompressedSize}");
        text.AppendLine($"Total bytes compressed : {manifest.TotalCompressedSize}");
        text.AppendLine();
        text.AppendLine("          Size Chunks File SHA                                 Flags Name");

        foreach (var file in manifest.Entries.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            var hash = Convert.ToHexStringLower(file.Hash);

            text.AppendLine(
                $"{file.Size,14} {file.Chunks.Count,6} {hash} {file.Flags,5} {file.Name}");
        }

        File.WriteAllText(path, text.ToString());
        return path;
    }
}
