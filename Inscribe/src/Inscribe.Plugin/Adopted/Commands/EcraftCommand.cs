// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;


namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// The one console command, <c>inscribe</c>, and its sub-command table (console-commands.md section 1). Registered
    /// without the game's <c>isCheat</c> flag (it would mark the player's profile as cheated) and without
    /// <c>onlyAdmin</c> / <c>onlyServer</c> (the game turns <c>onlyAdmin</c> into server-only); access is checked by
    /// <see cref="CommandAccess"/> on the machine that runs the command. Every sub-command runs on the caller's own
    /// machine (client, host, or a dedicated server's console) and changes at most the caller's inventory or files.
    /// Tab completion offers the sub-command names: the game completes only the word after the command.
    /// </summary>
    internal static class EcraftCommand
    {
        public const string Name = "inscribe";
        private static bool _registered;
        private static Terminal.ConsoleCommand? _command;
        internal static void Shutdown()
        {
            var field = HarmonyLib.AccessTools.Field(typeof(Terminal), "commands");
            if (field?.GetValue(null) is System.Collections.IDictionary commands && commands.Contains(Name) && ReferenceEquals(commands[Name], _command)) commands.Remove(Name);
            _command = null; _registered = false;
        }

        private static readonly List<SubCommand> Table = new List<SubCommand>
        {
            new SubCommand("help", "inscribe [help]", Access.ReadOnly, "lists the sub-commands you may run", HelpCommand.Run),
            new SubCommand("inspect", InspectCommand.Grammar, Access.ReadOnly, "an item's ecf_ data and its parse", InspectCommand.Run),
            new SubCommand("stats", "inscribe stats", Access.ReadOnly, "your aggregated inscription totals", StatsCommand.Run),
            new SubCommand("list", ListCommand.Grammar, Access.ReadOnly, "the running configuration", ListCommand.Run),
            new SubCommand("inscription", InscriptionCommand.Grammar, Access.ReadOnly, "one inscription with its tier rows and unlock levels", InscriptionCommand.Run),
            new SubCommand("classes", ClassesCommand.Grammar, Access.ReadOnly, "every item class: items, levels, pool size", ClassesCommand.Run),
            new SubCommand("cleanse", "inscribe cleanse", Access.Admin, "removes owned item inscriptions and rarity", Runtime.InscribeExtraCommands.Cleanse),
            new SubCommand("give", GiveCommand.Grammar, Access.Admin, "runes into your inventory", Runtime.InscribeExtraCommands.Give),
            new SubCommand("roll", "inscribe roll <rarity>", Access.Admin, "rolls the selected or hovered owned item", Runtime.InscribeExtraCommands.Roll),
            new SubCommand("reroll", RerollCommand.Grammar, Access.Admin, "rerolls every inscription of an item, keeping rarity", RerollCommand.Run),
            new SubCommand("inscribe", AffixCommand.Grammar, Access.Admin, "adds or replaces one inscription on an item", AffixCommand.Run),
            new SubCommand("reload", "inscribe reload", Access.Author, "re-reads the YAML, translations and .cfg now", ReloadCommand.Run),
            new SubCommand("dump", DumpCommand.Grammar, Access.Admin, "writes the merged configuration or the item survey", DumpCommand.Run),
            new SubCommand("tiers", "inscribe tiers", Access.Admin, "writes the item level reference file (levels 1-8)", TiersCommand.Run),
        };

        public static IReadOnlyList<SubCommand> SubCommands => Table;

        public static void Register()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            _command = new Terminal.ConsoleCommand(Name, "[help|inspect|stats|list|inscription|classes|give|roll|reroll|inscribe|reload|dump|tiers] - ExtraPotions.Inscribe.Adopted",
                (Terminal.ConsoleEvent)OnCommand, optionsFetcher: Names);
        }

        private static List<string> Names() => Table.ConvertAll(c => c.Name);

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            CommandCall call = new CommandCall(args);
            SubCommand? command = Table.Find(c => c.Name == call.Sub);
            if (command == null)
            {
                call.ReplyPlain($"{Name}: unknown sub-command '{call.Sub}'. '{Name} help' lists them.");
                return;
            }
            if (CommandAccess.Check(command, call))
            {
                Execute(command, call);
            }
        }

        // PatchGuard logs the exception under the mod's name and rethrows; the console gets one line.
        private static void Execute(SubCommand command, CommandCall call)
        {
            try
            {
                command.Run(call);
            }
            catch (Exception error)
            {
                Core.Log.Error(error.ToString()); call.Reply("failed, see the log.");
            }
        }
    }

    /// <summary>Who may run a sub-command (console-commands.md section 2).</summary>
    internal enum Access
    {
        Everyone,
        /// <summary>Everyone while <c>Read-only commands for everyone</c> is on, else admins.</summary>
        ReadOnly,
        Admin,
        /// <summary>Admins, and only where this machine's files are in force.</summary>
        Author,
    }

    /// <summary>One row of the sub-command table.</summary>
    internal sealed class SubCommand
    {
        public SubCommand(string name, string grammar, Access access, string summary, Action<CommandCall> run)
        {
            Name = name;
            Grammar = grammar;
            Access = access;
            Summary = summary;
            Run = run;
        }

        public string Name { get; }
        public string Grammar { get; }
        public Access Access { get; }
        public string Summary { get; }
        public Action<CommandCall> Run { get; }
    }
}
