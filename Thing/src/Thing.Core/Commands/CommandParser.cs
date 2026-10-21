// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the `thing` console verbs that run on the authority until the CS-D1 admin relay exists (design 7),
// and the player lookup they use. Moderation always keys on platform IDs; names only find an online player.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.WorldAdmin;
using ExtraPotions.Thing.Communication;

namespace ExtraPotions.Thing.Commands
{
    public enum CommandKind { Help, Status, Action, Usage, Unknown }

    public sealed class ParsedCommand
    {
        internal ParsedCommand(CommandKind kind, ThingAction action = ThingAction.ListPlayers, string target = "", string text = "", TimeSpan? duration = null, int minutes = 0, bool freeze = false, MessageEditRequest? message = null, bool topLeft = false, bool untilLifted = false, RegenerationRequest? regeneration = null, ExtraPotions.Thing.RemoteConfig.ConfigEditRequest? configFile = null)
        {
            Kind = kind;
            Action = action;
            Target = target;
            Text = text;
            Duration = duration;
            Minutes = minutes;
            Freeze = freeze;
            Message = message;
            TopLeft = topLeft;
            UntilLifted = untilLifted;
            Regeneration = regeneration;
            ConfigFile = configFile;
        }

        public CommandKind Kind { get; }

        /// <summary>The action for <see cref="CommandKind.Action"/> (and the verb's action for <see cref="CommandKind.Usage"/>).</summary>
        public ThingAction Action { get; }

        /// <summary>Player name, prefix or platform ID (unban: the platform ID).</summary>
        public string Target { get; }

        /// <summary>Reason or broadcast text; the usage line for Usage; the verb for Unknown.</summary>
        public string Text { get; }

        public TimeSpan? Duration { get; }

        public int Minutes { get; }
        public bool Freeze { get; }
        public MessageEditRequest? Message { get; }
        public bool TopLeft { get; }
        public bool UntilLifted { get; }
        public RegenerationRequest? Regeneration { get; }
        public ExtraPotions.Thing.RemoteConfig.ConfigEditRequest? ConfigFile { get; }
    }

    public static class CommandParser
    {
        public const string Name = "thing";

        static readonly Dictionary<string, string> Usage = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "kick", "thing kick <player> [reason]" },
            { "ban", "thing ban <player> [reason]" },
            { "tempban", "thing tempban <player> <duration: 90m, 24h, 3d> [reason]" },
            { "unban", "thing unban <platform id>" },
            { "warn", "thing warn <player> <reason>" },
            { "mute", "thing mute <player> [duration: 30m, 24h, 7d | forever] [reason]" },
            { "unmute", "thing unmute <player|platform id>" },
            { "goto", "thing goto <player|platform id>" },
            { "bring", "thing bring <player|platform id>" },
            { "say", "thing say <text>" },
            { "shutdown", "thing shutdown <minutes 1 to 120>" },
            { "cancelshutdown", "thing cancelshutdown" },
            { "save", "thing save" },
            { "motd", "thing motd get | set <source hash> <text> | clear <source hash>" },
            { "announce", "thing announce list [offset] | get <index> | add <hash> <minutes> <text> | set <hash> <index> <minutes> <text> | remove <hash> <index> | clear <hash>" },
            { "seed", "thing seed" },
            { "regeneration", "thing regeneration [kinds|actions|due|nearest|nature <offset>] | dryrun <kind> <on|off> <hash>" },
            { "regenerate", "thing regenerate <kind|nature> (nature: felled trees and bushes come back around you)" },
            { "resetdungeons", "thing resetdungeons [nearest] (default: all due dungeons)" },
            { "resetresources", "thing resetresources (all due dungeon resources, and mined rocks, ore and picked stones around you)" },
            { "time", "thing time <HH:mm|midnight|sunrise|noon|sunset> [freeze]" },
            { "resumetime", "thing resumetime" },
            { "permitted", "thing permitted [offset 0 to 9999]" },
            { "permit", "thing permit <platform id>" },
            { "unpermit", "thing unpermit <platform id>" },
            { "admins", "thing admins [offset 0 to 9999]" },
            { "admin", "thing admin <add|remove> <platform id>" },
            { "config", "thing config list [offset] | entries <file id> [offset] | get <file id> <entry id>" },
            { "set", "thing set <file id> <entry id> <version> <encoded value chunks>" },
        };

        /// <summary>The action verbs the console accepts in session 1, in help order.</summary>
        public static IReadOnlyList<string> ConsoleVerbs { get; } = Array.AsReadOnly(Usage.Keys.ToArray());

        public static ParsedCommand Parse(IReadOnlyList<string>? args)
        {
            var a = (args ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            if (a.Count == 0) return new ParsedCommand(CommandKind.Help);
            var verb = a[0].ToLowerInvariant();
            var topLeft = verb == "saytop";
            if (topLeft) verb = "say";
            if (verb == "help") return new ParsedCommand(CommandKind.Help);
            if (verb == "status") return new ParsedCommand(CommandKind.Status);
            if (verb == "ui-read")
                return a.Count == 3 && UI.ThingUiPage.AdminReadPage(a[1]) && int.TryParse(a[2], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                    && offset >= 0 && offset < UI.ThingUiPage.MaxTotal ? new ParsedCommand(CommandKind.Status)
                    : new ParsedCommand(CommandKind.Usage, text: "thing ui-read <players|permitted|admins|moderation|audit> <offset>");
            if (!Usage.ContainsKey(verb)) return new ParsedCommand(CommandKind.Unknown, text: a[0]);
            var action = ActionCatalog.FromVerb(verb)!.Action;
            var rest = a.Skip(1).ToList();
            ParsedCommand Bad() => new ParsedCommand(CommandKind.Usage, action, text: topLeft ? "thing saytop <text>" : Usage[verb]);
            string Join(int from) => string.Join(" ", rest.Skip(from));
            switch (verb)
            {
                case "config":
                case "set":
                    return ExtraPotions.Thing.RemoteConfig.ConfigEditRequest.TryParse(args!, out var configFile) ? new ParsedCommand(CommandKind.Action, configFile.Writes ? ThingAction.RemoteConfigWrite : ThingAction.RemoteConfigRead, configFile: configFile) : Bad();
                case "regeneration":
                    return RegenerationRequest.TryParse(a, out var regeneration) ? new ParsedCommand(CommandKind.Action, action, regeneration: regeneration) : Bad();
                case "resetdungeons":
                    return rest.Count == 0 ? new ParsedCommand(CommandKind.Action, action)
                        : rest.Count == 1 && rest[0].Equals("nearest", StringComparison.OrdinalIgnoreCase)
                            ? new ParsedCommand(CommandKind.Action, action, target: "nearest") : Bad();
                case "regenerate":
                    return rest.Count == 1 && (RegenerationRequest.KnownKind(rest[0]) || rest[0] == RegenerationRequest.Nature) ? new ParsedCommand(CommandKind.Action, action, target: rest[0]) : Bad();
                case "motd":
                case "announce":
                    return MessageEditing.TryParse(args!, out var message) ? new ParsedCommand(CommandKind.Action, action, message: message) : Bad();
                case "permitted":
                case "admins":
                    if (rest.Count == 0) return new ParsedCommand(CommandKind.Action, action);
                    return rest.Count == 1 && int.TryParse(rest[0], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                        && offset >= 0 && offset < 10000 ? new ParsedCommand(CommandKind.Action, action, minutes: offset) : Bad();
                case "permit":
                case "unpermit":
                    return rest.Count == 1 && GameListIdentity.Valid(rest[0]) ? new ParsedCommand(CommandKind.Action, action, rest[0]) : Bad();
                case "admin":
                    return rest.Count == 2 && (rest[0].Equals("add", StringComparison.OrdinalIgnoreCase) || rest[0].Equals("remove", StringComparison.OrdinalIgnoreCase))
                        && GameListIdentity.Valid(rest[1]) ? new ParsedCommand(CommandKind.Action, action, rest[1], rest[0].ToLowerInvariant()) : Bad();
                case "time":
                    return rest.Count >= 1 && rest.Count <= 2 && TimeOfDay.Parse(rest[0]).HasValue
                        && (rest.Count == 1 || rest[1].Equals("freeze", StringComparison.OrdinalIgnoreCase))
                        ? new ParsedCommand(CommandKind.Action, action, text: rest[0], freeze: rest.Count == 2) : Bad();
                case "kick":
                case "ban":
                    return rest.Count < 1 ? Bad() : new ParsedCommand(CommandKind.Action, action, rest[0], Join(1));
                case "goto":
                case "bring":
                    return rest.Count == 1 && rest[0].Length <= 160 && rest[0].IndexOfAny(new[] { '\0', '\r', '\n' }) < 0 ? new ParsedCommand(CommandKind.Action, action, rest[0]) : Bad();
                case "warn":
                    return rest.Count < 2 ? Bad() : new ParsedCommand(CommandKind.Action, action, rest[0], Join(1));
                case "mute":
                    if (rest.Count == 0) return Bad();
                    if (rest.Count == 1) return new ParsedCommand(CommandKind.Action, action, rest[0]);
                    var forever = rest[1].Equals("forever", StringComparison.OrdinalIgnoreCase);
                    var muteDuration = forever ? null : Durations.Parse(rest[1], DurationUnit.Minutes, 10080);
                    return !forever && muteDuration == null ? Bad() : new ParsedCommand(CommandKind.Action, action,
                        rest[0], Join(2), muteDuration, untilLifted: forever);
                case "tempban":
                    {
                        if (rest.Count < 2) return Bad();
                        var d = Durations.Parse(rest[1], DurationUnit.Hours, 8760);
                        return d == null ? Bad() : new ParsedCommand(CommandKind.Action, action, rest[0], Join(2), d);
                    }
                case "unban":
                case "unmute":
                    return rest.Count != 1 ? Bad() : new ParsedCommand(CommandKind.Action, action, rest[0]);
                case "say":
                    return rest.Count < 1 ? Bad() : new ParsedCommand(CommandKind.Action, action, text: Join(0), topLeft: topLeft);
                case "shutdown":
                    return rest.Count == 1 && int.TryParse(rest[0], NumberStyles.None, CultureInfo.InvariantCulture, out var m) && m >= 1 && m <= 120
                        ? new ParsedCommand(CommandKind.Action, action, minutes: m) : Bad();
                default:
                    return rest.Count == 0 ? new ParsedCommand(CommandKind.Action, action) : Bad();
            }
        }
    }

    public static class PlayerMatch
    {
        /// <summary>An online player by exact platform ID, else exact name (any case), else a unique name prefix; null otherwise.</summary>
        public static Actor? Find(string? query, IEnumerable<Actor> online)
        {
            var q = (query ?? "").Trim();
            if (q.Length == 0) return null;
            var list = online.ToList();
            foreach (var p in list) if (string.Equals(p.Id, q, StringComparison.Ordinal)) return p;
            var exact = list.Where(p => string.Equals(p.Name, q, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1) return null;
            var prefix = list.Where(p => p.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)).ToList();
            return prefix.Count == 1 ? prefix[0] : (Actor?)null;
        }
    }
}
