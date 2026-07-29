using SteamDepotDownload.Tier0.Shared.ConVar;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Logging;

namespace SteamDepotDownload.Tier0.Core.Logging;

internal sealed class CDefaultLoggingResponsePolicy : ILoggingResponsePolicy
{
    private ConVar<bool>? _noAssert;
    private bool _resolved;

    public LoggingResponse OnLog(LoggingContext context)
    {
        if (context.Severity == LoggingSeverity.Assert && !HasNoAssert())
        {
            return LoggingResponse.Debugger;
        }

        if (context.Severity == LoggingSeverity.Error)
        {
            return LoggingResponse.Abort;
        }

        return LoggingResponse.Continue;
    }

    private bool HasNoAssert()
    {
        if (!_resolved)
        {
            _noAssert = InterfaceSystem.GetInterface<IConVarSystem>(InterfaceNames.ConVar)?.Find("noassert") as ConVar<bool>;
            _resolved = true;
        }

        return _noAssert?.Value ?? false;
    }
}
