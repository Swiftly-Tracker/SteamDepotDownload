using SteamDepotDownload.Tier0.Shared.ConVar;
using SteamDepotDownload.Tier0.Shared.Interfaces;
using SteamDepotDownload.Tier0.Shared.Terminal;

namespace SteamDepotDownload.Tier0.Core.Terminal;

internal static class CTerminalCommands
{
    public static void Register()
    {
        _ = new ConCommand("help", Help, "Show all commands, or 'help <name>' for details on a command or convar.");
        _ = new ConCommand("cmds", Cmds, "Dump all commands.");
        _ = new ConCommand("convars", ConVars, "Dump all convars and their values.");
        _ = new ConCommand("echo", ctx => ctx.Print(ctx.ArgString), "Print the given text.");
        _ = new ConCommand("quit", ctx => ctx.Terminal.Stop(), "Exit the terminal.");
    }

    private static void Help(CommandContext ctx)
    {
        if (ctx.Args.Length == 0)
        {
            Cmds(ctx);
            return;
        }

        string target = ctx.Args[0];

        var command = ctx.Terminal.FindCommand(target);
        if (command != null)
        {
            ctx.Print($"{command.Name} (command) - {command.Description ?? ""}");
            return;
        }

        var convar = ConVarSystem()?.Find(target);
        if (convar != null)
        {
            ctx.Print($"{convar.Name} (convar {convar.ValueType.Name}) = {convar.ToStringValue()}");
            if (convar.Description != null)
            {
                ctx.Print($"  {convar.Description}");
            }
            if (convar.Flags != ConVarFlags.None)
            {
                ctx.Print($"  flags: {convar.Flags}");
            }
            if (convar.HasBounds)
            {
                ctx.Print("  bounded");
            }
            return;
        }

        ctx.Warn($"Unknown command or convar: '{target}'");
    }

    private static void Cmds(CommandContext ctx)
    {
        ctx.Print("Commands:");
        foreach (var command in ctx.Terminal.GetCommands().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            ctx.Print($"  {command.Name} - {command.Description ?? ""}");
        }
    }

    private static void ConVars(CommandContext ctx)
    {
        var convars = ConVarSystem();
        if (convars == null)
        {
            return;
        }

        ctx.Print("ConVars:");
        foreach (var convar in convars.GetAll().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            ctx.Print($"  {convar.Name} = {convar.ToStringValue()}{(convar.Description != null ? $"  ({convar.Description})" : "")}");
        }
    }

    private static IConVarSystem? ConVarSystem()
        => InterfaceSystem.GetInterface<IConVarSystem>(InterfaceNames.ConVar);
}
