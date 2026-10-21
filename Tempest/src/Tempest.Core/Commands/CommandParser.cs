// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: console verbs of design 5 and W7 (WildWeather's verbs, behaviour only) and the
// personal override of W9 (Valheim_Admin_Tool's local weather override, behaviour only).
using System;
using System.Linq;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Commands
{
    /// <summary>Console verbs. The numbers are sent to the server (<see cref="RemoteCommand"/>), so new verbs are appended.</summary>
    public enum CommandVerb { Help, Status, Blizzard, Storm, Stop, Override, OverrideReset, DenseFog, AshStorm, MeteorShower, Import, ImportApply }

    public sealed class ParsedCommand
    {
        internal ParsedCommand(CommandVerb verb, bool now = false, string? environment = null, bool freeze = false, bool dry = false)
        {
            Dry = dry;
            Verb = verb;
            Now = now;
            Environment = environment;
            Freeze = freeze;
        }

        public CommandVerb Verb { get; }

        /// <summary>The event kind a summon verb starts (a storm with <see cref="Dry"/> is a Lightning Storm), or null.</summary>
        public EventKind? Kind => CommandParser.KindOf(Verb, Dry);

        /// <summary>Summon without the warning phase.</summary>
        public bool Now { get; }

        /// <summary>Summon a lightning storm (storm only; <c>lightning</c>, or its alias <c>dry</c>).</summary>
        public bool Dry { get; }

        /// <summary>The environment name of an override.</summary>
        public string? Environment { get; }

        /// <summary>Keep the override until reset (otherwise it lasts one game weather period).</summary>
        public bool Freeze { get; }
    }

    /// <summary>
    /// <c>tempest status | blizzard [now] | storm [now] [lightning] | lightningstorm [now] | fog [now] | ashstorm [now] |
    /// meteorshower [now] | stop | override &lt;environment&gt; [freeze] | override reset</c>. <c>lightningstorm</c> is
    /// <c>storm lightning</c> (so an older server still understands it); <c>densefog</c>, <c>ash</c>, <c>meteors</c> and
    /// <c>meteor</c> are aliases. <c>import [apply]</c> previews or applies the NoFogBruh settings import (host or single
    /// player). Anything else gives Help.
    /// </summary>
    public static class CommandParser
    {
        public const string Name = "tempest";

        public static ParsedCommand Parse(string[]? args)
        {
            var a = (args ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            if (a.Length == 0) return new ParsedCommand(CommandVerb.Status);
            var verb = a[0].ToLowerInvariant();
            switch (verb)
            {
                case "status": return a.Length == 1 ? new ParsedCommand(CommandVerb.Status) : Help;
                case "stop": return a.Length == 1 ? new ParsedCommand(CommandVerb.Stop) : Help;
                case "import":
                    if (a.Length == 1) return new ParsedCommand(CommandVerb.Import);
                    return a.Length == 2 && a[1].Equals("apply", StringComparison.OrdinalIgnoreCase) ? new ParsedCommand(CommandVerb.ImportApply) : Help;
                case "lightningstorm":
                case "lightning":
                case "fog":
                case "densefog":
                case "ashstorm":
                case "ash":
                case "meteorshower":
                case "meteors":
                case "meteor":
                {
                    if (a.Length > 2 || (a.Length == 2 && !a[1].Equals("now", StringComparison.OrdinalIgnoreCase))) return Help;
                    var now = a.Length == 2;
                    switch (verb)
                    {
                        case "lightningstorm":
                        case "lightning": return new ParsedCommand(CommandVerb.Storm, now, dry: true);
                        case "fog":
                        case "densefog": return new ParsedCommand(CommandVerb.DenseFog, now);
                        case "ashstorm":
                        case "ash": return new ParsedCommand(CommandVerb.AshStorm, now);
                        default: return new ParsedCommand(CommandVerb.MeteorShower, now);
                    }
                }
                case "blizzard":
                case "storm":
                case "thunderstorm":
                {
                    var storm = verb != "blizzard";
                    bool now = false, dry = false;
                    foreach (var option in a.Skip(1))
                    {
                        if (option.Equals("now", StringComparison.OrdinalIgnoreCase) && !now) now = true;
                        else if (storm && (option.Equals("lightning", StringComparison.OrdinalIgnoreCase) || option.Equals("dry", StringComparison.OrdinalIgnoreCase)) && !dry) dry = true;
                        else return Help;
                    }
                    return new ParsedCommand(storm ? CommandVerb.Storm : CommandVerb.Blizzard, now, dry: dry);
                }
                case "override":
                {
                    if (a.Length == 2 && a[1].Equals("reset", StringComparison.OrdinalIgnoreCase)) return new ParsedCommand(CommandVerb.OverrideReset);
                    if (a.Length < 2 || a.Length > 3 || (a.Length == 3 && !a[2].Equals("freeze", StringComparison.OrdinalIgnoreCase))) return Help;
                    return new ParsedCommand(CommandVerb.Override, environment: a[1], freeze: a.Length == 3);
                }
                default: return Help;
            }
        }

        static ParsedCommand Help => new ParsedCommand(CommandVerb.Help);

        /// <summary>Verbs that start an event.</summary>
        public static bool Summons(CommandVerb verb) => KindOf(verb, false) != null;

        /// <summary>The kind a summon verb starts, or null for other verbs.</summary>
        public static EventKind? KindOf(CommandVerb verb, bool dry)
        {
            switch (verb)
            {
                case CommandVerb.Blizzard: return EventKind.Blizzard;
                case CommandVerb.Storm: return dry ? EventKind.LightningStorm : EventKind.Thunderstorm;
                case CommandVerb.DenseFog: return EventKind.DenseFog;
                case CommandVerb.AshStorm: return EventKind.AshStorm;
                case CommandVerb.MeteorShower: return EventKind.MeteorShower;
                default: return null;
            }
        }
    }

    public enum CommandRefusal { None, CheatOff, Headless }

    /// <summary>Where a verb runs: on this machine, or sent to the server through <see cref="RemoteCommand"/>.</summary>
    public enum CommandRoute { Local, Remote }

    /// <summary>
    /// Who may run a verb (design 5). Status, help and override reset always. Summon and stop need Weather Commands on (the
    /// effective cheat value already folds in C02, C03 and the server value); on a remote client they are sent to the
    /// server through the temporary <see cref="RemoteCommand"/> path, where admin and the Mischief gate are decided. The
    /// override needs Personal Weather Override on and a sky to draw (not on a dedicated server).
    /// </summary>
    public static class CommandRules
    {
        public static CommandRefusal Decide(CommandVerb verb, SessionFacts facts, bool weatherCommands, bool personalOverride)
        {
            switch (verb)
            {
                case CommandVerb.Blizzard:
                case CommandVerb.Storm:
                case CommandVerb.DenseFog:
                case CommandVerb.AshStorm:
                case CommandVerb.MeteorShower:
                case CommandVerb.Stop:
                    return weatherCommands ? CommandRefusal.None : CommandRefusal.CheatOff;
                case CommandVerb.Override:
                    if (facts.Kind == SessionKind.DedicatedServer) return CommandRefusal.Headless;
                    return personalOverride ? CommandRefusal.None : CommandRefusal.CheatOff;
                default:
                    return CommandRefusal.None;
            }
        }

        /// <summary>Summon and stop from a remote client go to the server; everything else runs here.</summary>
        public static CommandRoute Route(CommandVerb verb, SessionFacts facts) =>
            !facts.IsAuthority && RemoteCommand.Sendable(verb) ? CommandRoute.Remote : CommandRoute.Local;
    }
}
