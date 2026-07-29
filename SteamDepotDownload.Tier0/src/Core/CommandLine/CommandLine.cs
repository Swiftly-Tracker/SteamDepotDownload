using SteamDepotDownload.Tier0.Shared.CommandLine;
using SteamDepotDownload.Tier0.Shared.ConVar;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Terminal;

namespace SteamDepotDownload.Tier0.Core.CommandLine;

[ExposeInterface(InterfaceNames.CommandLine)]
internal class CCommandLine : ICommandLine
{
    private string _commandLine = "";
    private List<string> _parameters = [];
    private Dictionary<string, string> _parameterValues = [];

    public ulong ParameterCount => (ulong)_parameters.Count;

    public List<string> Parameters => _parameters;

    public string CommandLineString => _commandLine;

    public string GetParameterValue(string parameter, string defaultValue = "")
    {
        if (!_parameterValues.TryGetValue(parameter, out var value))
        {
            return defaultValue;
        }
        return value;
    }

    public bool HasParameter(string parameter)
    {
        return _parameterValues.ContainsKey(parameter);
    }

    public void Initialize(string commandLine)
    {
        _commandLine = commandLine;
        _parameters = [.. commandLine.Split(' ')];
        _parameterValues = [];
        for (int i = 0; i < _parameters.Count; i++)
        {
            if (_parameters[i].StartsWith('-'))
            {
                var key = _parameters[i][1..];
                if (i + 1 < _parameters.Count && !_parameters[i + 1].StartsWith('-'))
                {
                    _parameterValues[key] = _parameters[i + 1];
                    i++;
                }
                else
                {
                    _parameterValues[key] = "";
                }
            }
        }

        ApplyStartupConfig();
    }

    private void ApplyStartupConfig()
    {
        var convars = InterfaceSystem.GetInterface<IConVarSystem>(InterfaceNames.ConVar);
        var terminal = InterfaceSystem.GetInterface<ITerminal>(InterfaceNames.Terminal);

        for (int i = 0; i < _parameters.Count; i++)
        {
            var token = _parameters[i];
            if (token.Length < 2 || (token[0] != '+' && token[0] != '-'))
            {
                continue;
            }

            bool force = token[0] == '-';
            string name = token[1..];

            var parts = new List<string>();
            int j = i + 1;
            while (j < _parameters.Count && !IsDirective(_parameters[j]))
            {
                parts.Add(_parameters[j]);
                j++;
            }
            i = j - 1;

            string? value = parts.Count > 0 ? string.Join(' ', parts) : null;

            var convar = convars?.Find(name);
            if (convar != null)
            {
                if (!force && convar.Flags.HasFlag(ConVarFlags.ReadOnly))
                {
                    continue;
                }

                if (value == null)
                {
                    if (convar.ValueType != typeof(bool))
                    {
                        continue;
                    }
                    value = "1";
                }

                convar.SetFromString(value, force);
            }
            else if (!force)
            {
                var line = value == null ? name : $"{name} {value}";
                terminal?.Execute(line);
            }
        }
    }

    private static bool IsDirective(string token)
        => token.Length >= 2 && (token[0] == '+' || token[0] == '-');

    public void Initialize(string[] args)
    {
        Initialize(string.Join(' ', args));
    }

    public void InitializeWithAppName(string commandLine)
    {
        var args = commandLine.Split(' ');
        if (args.Length > 0)
        {
            var appName = args[0];
            var remainingArgs = args[1..];
            Initialize(string.Join(' ', remainingArgs));
        }
        else
        {
            Initialize(commandLine);
        }
    }
}