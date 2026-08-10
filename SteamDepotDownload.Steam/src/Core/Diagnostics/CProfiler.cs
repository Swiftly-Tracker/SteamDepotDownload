using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SteamDepotDownload.Steam.Core.Diagnostics;

public static class CProfiler
{
    private static readonly ConcurrentDictionary<string, Stat> Stats = new(StringComparer.Ordinal);

    public static bool Enabled { get; set; }

    public static Scope Measure([CallerMemberName] string? member = null, [CallerFilePath] string? file = null)
        => Enabled ? new Scope(Label(file, member)) : default;

    public static void PrintSummary()
    {
        if (Stats.IsEmpty)
        {
            return;
        }

        var rows = Stats
            .Select(pair => (Label: pair.Key, Stat: pair.Value))
            .OrderByDescending(row => row.Stat.TotalTicks)
            .ToList();

        Console.WriteLine();
        Console.WriteLine("Profiler summary:");
        Console.WriteLine($"  {"function",-48}{"calls",8}{"total",12}{"avg",12}{"min",12}{"max",12}");

        foreach (var (label, stat) in rows)
        {
            var count = Math.Max(1, stat.Count);

            Console.WriteLine($"  {label,-48}{stat.Count,8}{Format(stat.TotalTicks),12}" +
                $"{Format(stat.TotalTicks / count),12}{Format(stat.MinTicks),12}{Format(stat.MaxTicks),12}");
        }
    }

    private static string Label(string? file, string? member)
    {
        var type = string.IsNullOrEmpty(file) ? "?" : Path.GetFileNameWithoutExtension(file);
        return $"{type}.{member}";
    }

    private static void Record(string label, long elapsedTicks)
    {
        var stat = Stats.GetOrAdd(label, static _ => new Stat());

        Interlocked.Increment(ref stat.Count);
        Interlocked.Add(ref stat.TotalTicks, elapsedTicks);
        UpdateExtreme(ref stat.MinTicks, elapsedTicks, min: true);
        UpdateExtreme(ref stat.MaxTicks, elapsedTicks, min: false);
    }

    private static void UpdateExtreme(ref long location, long value, bool min)
    {
        var current = Volatile.Read(ref location);

        while (min ? value < current : value > current)
        {
            var previous = Interlocked.CompareExchange(ref location, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static string Format(long rawTicks)
    {
        var ms = Stopwatch.GetElapsedTime(0, rawTicks).TotalMilliseconds;

        return ms >= 1000 ? $"{ms / 1000.0:0.###}s" : $"{ms:0.##}ms";
    }

    private sealed class Stat
    {
        internal long Count;
        internal long TotalTicks;
        internal long MinTicks = long.MaxValue;
        internal long MaxTicks;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly string? _label;
        private readonly long _start;

        internal Scope(string label)
        {
            _label = label;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_label == null)
            {
                return;
            }

            Record(_label, Stopwatch.GetTimestamp() - _start);
        }
    }
}
