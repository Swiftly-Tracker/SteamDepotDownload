using SteamDepotDownload.Steam.Shared.CommandLine;
using SteamDepotDownload.Steam.Shared.Depot;
using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Steam.Core.Depot;

[BinaryContract]
internal sealed class CDepotStateFile
{
    public const int CurrentVersion = 1;

    [BinaryMember(1)]
    public int Version { get; set; } = CurrentVersion;

    [BinaryMember(2)]
    public string? Tool { get; set; }

    [BinaryMember(3)]
    public DateTimeOffset Updated { get; set; }

    [BinaryMember(4)]
    public Dictionary<uint, CDepotStateEntry> Depots { get; set; } = [];
}

[BinaryContract]
internal sealed class CDepotStateEntry
{
    [BinaryMember(1)]
    public ulong Manifest { get; set; }

    [BinaryMember(2)]
    public uint App { get; set; }

    [BinaryMember(3)]
    public string Branch { get; set; } = DepotConstants.PublicBranch;

    [BinaryMember(4)]
    public uint Build { get; set; }

    [BinaryMember(5)]
    public string? Name { get; set; }

    [BinaryMember(6)]
    public ulong Bytes { get; set; }

    [BinaryMember(7)]
    public int Files { get; set; }

    [BinaryMember(8)]
    public DateTimeOffset Updated { get; set; }
}

internal sealed class CDepotStateStore : IDepotStateStore
{
    private readonly Lock _lock = new();
    private readonly string _path;
    private readonly Dictionary<uint, DepotInstallRecord> _records = [];
    private bool _dirty;

    internal CDepotStateStore(string path)
    {
        _path = path;
        Load();
    }

    internal static string PathFor(string installDirectory)
        => Path.Combine(installDirectory, DepotConstants.ConfigDirectory, DepotConstants.DepotStateFile);

    public ulong GetInstalledManifest(uint depotId)
    {
        lock (_lock)
        {
            return _records.TryGetValue(depotId, out var record)
                ? record.ManifestId
                : DepotConstants.InvalidManifestId;
        }
    }

    public DepotInstallRecord? GetRecord(uint depotId)
    {
        lock (_lock)
        {
            return _records.GetValueOrDefault(depotId);
        }
    }

    public IReadOnlyList<DepotInstallRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return [.. _records.Values];
            }
        }
    }

    public void SetInstalledManifest(uint depotId, ulong manifestId)
        => Record(new DepotInstallRecord
        {
            DepotId = depotId,
            ManifestId = manifestId,
            UpdatedUtc = DateTimeOffset.UtcNow,
        });

    public void Record(DepotInstallRecord record)
    {
        lock (_lock)
        {
            _records[record.DepotId] = record;
            _dirty = true;
        }
    }

    public void Remove(uint depotId)
    {
        lock (_lock)
        {
            if (_records.Remove(depotId))
            {
                _dirty = true;
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            if (!_dirty)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new CDepotStateFile
            {
                Tool = $"SteamDepotDownload/{DepotUsage.Version}",
                Updated = DateTimeOffset.UtcNow,
            };

            foreach (var record in _records.Values)
            {
                file.Depots[record.DepotId] = new CDepotStateEntry
                {
                    Manifest = record.ManifestId,
                    App = record.AppId,
                    Branch = record.Branch,
                    Build = record.BuildId,
                    Name = record.Name,
                    Bytes = record.SizeOnDisk,
                    Files = record.FileCount,
                    Updated = record.UpdatedUtc,
                };
            }

            var temp = $"{_path}.{Environment.ProcessId:x}-{Environment.CurrentManagedThreadId:x}.tmp";

            using (var stream = File.Create(temp))
            {
                BinaryFormat.Serialize(stream, file);
            }

            File.Move(temp, _path, overwrite: true);

            _dirty = false;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        CDepotStateFile? file;

        try
        {
            using var stream = File.OpenRead(_path);

            file = BinaryFormat.Deserialize<CDepotStateFile>(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or BinaryFormatException or EndOfStreamException)
        {
            return;
        }

        if (file == null || file.Version != CDepotStateFile.CurrentVersion)
        {
            return;
        }

        foreach (var (depotId, entry) in file.Depots)
        {
            _records[depotId] = new DepotInstallRecord
            {
                DepotId = depotId,
                ManifestId = entry.Manifest,
                AppId = entry.App,
                Branch = entry.Branch,
                BuildId = entry.Build,
                Name = entry.Name,
                SizeOnDisk = entry.Bytes,
                FileCount = entry.Files,
                UpdatedUtc = entry.Updated,
            };
        }
    }
}
