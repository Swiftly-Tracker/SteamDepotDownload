using System.Runtime.CompilerServices;
using SteamDepotDownload.Steam.Core.Interfaces;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Logging;

namespace SteamDepotDownload.Steam.Core.Diagnostics;

internal static class CSteamLog
{
    private static ILoggingSystem? _log;
    private static bool _resolved;

    private static int _steam = -1;
    private static int _depot = -1;
    private static int _cdn = -1;

    internal static int Steam => Channel(ref _steam, CSteamModule.SteamChannel);

    internal static int Depot => Channel(ref _depot, CSteamModule.DepotChannel);

    internal static int Cdn => Channel(ref _cdn, CSteamModule.CdnChannel);

    internal static ILoggingSystem? System
    {
        get
        {
            if (!_resolved)
            {
                _log = InterfaceSystem.GetInterface<ILoggingSystem>(InterfaceNames.LoggingSystem);
                _resolved = true;
            }

            return _log;
        }
    }

    internal static void Msg(int channel, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => System?.Msg(channel, message, file, line, function);

    internal static void Detailed(int channel, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => System?.DetailedMsg(channel, message, file, line, function);

    internal static void Warning(int channel, string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => System?.Warning(channel, message, file, line, function);

    internal static ILoggingTask? BeginProgress(int channel, string label,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => System?.BeginProgress(channel, label, file, line, function);

    internal static ILoggingTask? BeginSpinner(int channel, string label,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? function = null)
        => System?.BeginSpinner(channel, label, file, line, function);

    private static int Channel(ref int cached, string name)
    {
        if (cached < 0)
        {
            cached = System?.FindChannel(name) ?? -1;
        }

        return cached;
    }
}
