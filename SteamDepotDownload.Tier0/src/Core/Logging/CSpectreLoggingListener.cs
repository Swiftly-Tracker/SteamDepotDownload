using System.Text;
using SteamDepotDownload.Tier0.Shared.Drawing;
using SteamDepotDownload.Tier0.Shared.Logging;
using SteamDepotDownload.Tier0.Shared.Terminal;
using Spectre.Console;

namespace SteamDepotDownload.Tier0.Core.Logging;

internal sealed class CSpectreLoggingListener : ILoggingListener
{
    public void Log(LoggingContext context, string message)
    {
        if (context.Flags.HasFlag(LoggingChannelFlags.DoNotEcho))
        {
            return;
        }

        string markup = BuildMarkup(context, message);

        var writer = TerminalOutput.Writer;
        if (writer != null)
        {
            writer(markup);
        }
        else
        {
            AnsiConsole.MarkupLine(markup);
        }
    }

    private static string BuildMarkup(LoggingContext context, string message)
    {
        var sb = new StringBuilder();

        string channelColor = context.Color.A == 0
            ? SeverityColor(context.Severity)
            : context.Color.ToHex();
        sb.Append($"[{channelColor}][[{Markup.Escape(context.ChannelName)}]][/] ");

        if (context.Verbosity >= LoggingVerbosity.Detailed && !string.IsNullOrEmpty(context.Source.File))
        {
            string path = $"{ShortFile(context.Source.File)}:{context.Source.Line}";
            string source = string.IsNullOrEmpty(context.Source.Function)
                ? path
                : $"{path} - {context.Source.Function}";
            sb.Append($"[grey][[{Markup.Escape(source)}]][/] ");
        }

        string? messageColor = MessageColor(context.Severity);
        if (messageColor != null)
        {
            sb.Append($"[{messageColor}]{Markup.Escape(message)}[/]");
        }
        else
        {
            sb.Append(Markup.Escape(message));
        }

        return sb.ToString();
    }

    private static string SeverityColor(LoggingSeverity severity) => severity switch
    {
        LoggingSeverity.Detailed => "grey",
        LoggingSeverity.Message => "silver",
        LoggingSeverity.Warning => "yellow",
        LoggingSeverity.Assert => "magenta1",
        LoggingSeverity.Error => "red",
        _ => "silver",
    };

    private static string? MessageColor(LoggingSeverity severity) => severity switch
    {
        LoggingSeverity.Warning => "yellow",
        LoggingSeverity.Assert => "magenta1",
        LoggingSeverity.Error => "red",
        _ => null,
    };

    private static string ShortFile(string file)
    {
        string normalized = file.Replace('\\', '/');
        int index = normalized.LastIndexOf("/src/", StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? normalized[(index + 1)..] : Path.GetFileName(normalized);
    }
}
