namespace SteamDepotDownload.Tier0.Shared.CommandLine;

public interface ICommandLine
{
    public void Initialize(string commandLine);
    public void Initialize(string[] args);
    public void InitializeWithAppName(string commandLine);

    public bool HasParameter(string parameter);
    public string GetParameterValue(string parameter, string defaultValue = "");

    public ulong ParameterCount { get; }
    public List<string> Parameters { get; }
    public string CommandLineString { get; }
}