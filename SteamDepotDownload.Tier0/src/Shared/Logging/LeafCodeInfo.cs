namespace SteamDepotDownload.Tier0.Shared.Logging;

public readonly struct LeafCodeInfo
{
    public string? File { get; }
    public int Line { get; }
    public string? Function { get; }

    public LeafCodeInfo(string? file, int line, string? function)
    {
        File = file;
        Line = line;
        Function = function;
    }
}
