using SteamDepotDownload.Steam.Core.Diagnostics;
using SteamDepotDownload.Steam.Shared.Auth;
using SteamDepotDownload.Tier0.Shared.Serialization;

namespace SteamDepotDownload.Steam.Core.Auth;

[BinaryContract]
internal sealed class CAccountSettingsData
{
    [BinaryMember(1)]
    public Dictionary<string, int> ContentServerPenalty { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [BinaryMember(2)]
    public Dictionary<string, string> LoginTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [BinaryMember(3)]
    public Dictionary<string, string> GuardData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class CFileAccountSettingsStore : IAccountSettingsStore
{
    private readonly Lock _lock = new();
    private readonly string _path;
    private CAccountSettingsData _data = new();

    public CFileAccountSettingsStore() : this(DefaultPath)
    {
    }

    public CFileAccountSettingsStore(string path)
    {
        _path = path;
        Load();
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify),
        "SteamDepotDownload",
        "account.config");

    public string? GetRefreshToken(string account)
    {
        lock (_lock)
        {
            return _data.LoginTokens.TryGetValue(account, out var token) ? token : null;
        }
    }

    public void SetRefreshToken(string account, string token)
    {
        lock (_lock)
        {
            _data.LoginTokens[account] = token;
        }
    }

    public void RemoveRefreshToken(string account)
    {
        lock (_lock)
        {
            _data.LoginTokens.Remove(account);
        }
    }

    public string? GetGuardData(string account)
    {
        lock (_lock)
        {
            return _data.GuardData.TryGetValue(account, out var data) ? data : null;
        }
    }

    public void SetGuardData(string account, string data)
    {
        lock (_lock)
        {
            _data.GuardData[account] = data;
        }
    }

    public int GetContentServerPenalty(string host)
    {
        lock (_lock)
        {
            return _data.ContentServerPenalty.TryGetValue(host, out var penalty) ? penalty : 0;
        }
    }

    public void SetContentServerPenalty(string host, int penalty)
    {
        lock (_lock)
        {
            _data.ContentServerPenalty[host] = penalty;
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);

                RestrictToOwner(directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var temp = _path + ".tmp";

            using (var file = File.Create(temp))
            {
                RestrictToOwner(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);

                BinaryFormat.Serialize(file, _data);
            }

            File.Move(temp, _path, overwrite: true);
        }
    }

    private static void RestrictToOwner(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, mode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CSteamLog.Warning(CSteamLog.Steam,
                $"Could not restrict permissions on {path}, so its Steam tokens may be readable " +
                $"by other local accounts: {ex.Message}");
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            using var file = File.OpenRead(_path);

            _data = BinaryFormat.Deserialize<CAccountSettingsData>(file) ?? new CAccountSettingsData();
            Normalize();
        }
        catch (Exception ex) when (ex is IOException or BinaryFormatException or EndOfStreamException)
        {
            _data = new CAccountSettingsData();
        }
    }

    private void Normalize()
    {
        _data.ContentServerPenalty = new Dictionary<string, int>(_data.ContentServerPenalty, StringComparer.OrdinalIgnoreCase);
        _data.LoginTokens = new Dictionary<string, string>(_data.LoginTokens, StringComparer.OrdinalIgnoreCase);
        _data.GuardData = new Dictionary<string, string>(_data.GuardData, StringComparer.OrdinalIgnoreCase);
    }
}
