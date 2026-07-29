using System.Text.Json;
using System.Text.RegularExpressions;

namespace SteamDepotDownload.Steam.Shared.Depot;

public sealed class FileFilter
{
    private readonly HashSet<string> _literals = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Regex> _patterns = [];
    private readonly Dictionary<uint, FileFilter>? _perDepot;

    private FileFilter()
    {
    }

    private FileFilter(Dictionary<uint, FileFilter> perDepot) => _perDepot = perDepot;

    public bool IsEmpty => _perDepot == null
        ? _literals.Count == 0 && _patterns.Count == 0
        : _perDepot.Count == 0;

    public bool IsPerDepot => _perDepot != null;

    public IReadOnlyCollection<uint> DepotIds => (IReadOnlyCollection<uint>?)_perDepot?.Keys ?? [];

    public IReadOnlyCollection<string> Literals => _literals;

    public IReadOnlyList<Regex> Patterns => _patterns;

    public static FileFilter FromLines(IEnumerable<string> lines)
    {
        var filter = new FileFilter();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                var pattern = line["regex:".Length..].Trim();
                if (pattern.Length > 0)
                {
                    filter._patterns.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                }

                continue;
            }

            filter._literals.Add(Normalize(line));
        }

        return filter;
    }

    public static FileFilter FromDepotMap(IReadOnlyDictionary<uint, IEnumerable<string>> rules)
        => new(rules.ToDictionary(entry => entry.Key, entry => FromLines(entry.Value)));

    public static FileFilter FromFile(string path)
    {
        var text = File.ReadAllText(path);

        return text.AsSpan().TrimStart() is ['{', ..]
            ? FromJson(text)
            : FromLines(text.Split('\n'));
    }

    public static FileFilter FromJson(string json)
    {
        Dictionary<string, string[]>? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
        }
        catch (JsonException ex)
        {
            throw new DepotDownloadException(
                $"The file list is not a valid depot map: {ex.Message} " +
                "Expected an object of depot id to an array of rules.");
        }

        if (parsed == null)
        {
            throw new DepotDownloadException("The file list is empty.");
        }

        var rules = new Dictionary<uint, IEnumerable<string>>();

        foreach (var (key, value) in parsed)
        {
            if (!uint.TryParse(key, out var depotId))
            {
                throw new DepotDownloadException($"'{key}' is not a depot id.");
            }

            rules[depotId] = value ?? [];
        }

        return FromDepotMap(rules);
    }

    public bool Covers(uint depotId) => _perDepot == null || _perDepot.ContainsKey(depotId);

    public bool IsIncluded(uint depotId, string manifestPath)
    {
        if (_perDepot == null)
        {
            return IsIncluded(manifestPath);
        }

        return _perDepot.TryGetValue(depotId, out var filter) && filter.IsIncluded(manifestPath);
    }

    public bool IsIncluded(string manifestPath)
    {
        if (_perDepot != null)
        {
            return _perDepot.Values.Any(filter => filter.IsIncluded(manifestPath));
        }

        if (IsEmpty)
        {
            return true;
        }

        var normalized = Normalize(manifestPath);

        if (_literals.Contains(normalized))
        {
            return true;
        }

        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(normalized))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
