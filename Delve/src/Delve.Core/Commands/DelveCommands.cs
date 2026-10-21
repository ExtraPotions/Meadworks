// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/AdminCommands.cs: one prefixed word carried by the game's kick command, Handle's verbs and the help lines). The MIT
// notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: verbs renamed delve-* (design 12.1), parsed into a command value; regenerate-now follows the
// Regenerate cheat verdict (user decision D1: the delay is skipped only while the cheat is on and allowed, otherwise
// the kinds are evaluated now with their delays); get and set verbs are not adopted (config editing belongs to Thing).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Surface;

namespace ExtraPotions.Delve.Commands
{
    public enum CommandVerb { Help, Status, Regenerate, Surface }

    public enum RegenerateMode { SkipDelay, EvaluateNow, Refused }

    public sealed class DelveCommand
    {
        internal DelveCommand(CommandVerb verb, IReadOnlyList<DelveKind>? kinds = null, SurfaceScope scope = SurfaceScope.All)
        {
            Verb = verb;
            Kinds = kinds ?? Array.Empty<DelveKind>();
            Scope = scope;
        }

        public CommandVerb Verb { get; }
        public IReadOnlyList<DelveKind> Kinds { get; }

        /// <summary>For <see cref="CommandVerb.Surface"/>: what the surface reset brings back.</summary>
        public SurfaceScope Scope { get; }
    }

    /// <summary>
    /// A server-only mod cannot add console commands (the console runs on the player's machine), but "kick &lt;word&gt;"
    /// reaches the server's ZNet.RPC_Kick, which checks adminlist.txt. Words starting with "delve-" are taken before
    /// anyone is kicked. Only the first word after kick is sent, hence the one-word syntax.
    /// </summary>
    public static class DelveCommands
    {
        public const string Prefix = "delve-";

        public static readonly IReadOnlyDictionary<string, DelveKind[]> KindWords = new Dictionary<string, DelveKind[]>(StringComparer.Ordinal)
        {
            ["chests"] = new[] { DelveKind.Chests },
            ["mudpiles"] = new[] { DelveKind.MudPiles },
            ["crystals"] = new[] { DelveKind.FrostCaveCrystals },
            ["blackcores"] = new[] { DelveKind.BlackCores },
            ["morkhalla"] = new[] { DelveKind.Morkhalla },
            ["hole"] = new[] { DelveKind.Hole },
            ["queen"] = new[] { DelveKind.Queen },
            ["kall"] = new[] { DelveKind.Kall },
            ["armor"] = new[] { DelveKind.AncientArmor },
            ["obsidian"] = new[] { DelveKind.Obsidian },
            ["flametal"] = new[] { DelveKind.Flametal },
            ["ice"] = new[] { DelveKind.Ice },
            ["fullreset"] = new[] { DelveKind.FullReset },
            ["spawners"] = new[] { DelveKind.CampSpawners, DelveKind.DungeonSpawners },
        };

        /// <summary>The command in one kick word, or null when the word is not a Delve command (a real kick).</summary>
        public static DelveCommand? Parse(string? word)
        {
            var w = (word ?? "").Trim().ToLowerInvariant();
            if (!w.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var verb = w.Substring(Prefix.Length);
            if (verb == "status") return new DelveCommand(CommandVerb.Status);
            if (verb == "regen") return new DelveCommand(CommandVerb.Regenerate, Enum.GetValues(typeof(DelveKind)).Cast<DelveKind>().ToArray());
            if (verb.StartsWith("regen-", StringComparison.Ordinal) && KindWords.TryGetValue(verb.Substring("regen-".Length), out var kinds))
                return new DelveCommand(CommandVerb.Regenerate, kinds);
            if (verb == "surface") return new DelveCommand(CommandVerb.Surface, scope: SurfaceScope.All);
            if (verb == "surface-resources") return new DelveCommand(CommandVerb.Surface, scope: SurfaceScope.Resources);
            if (verb == "surface-nature") return new DelveCommand(CommandVerb.Surface, scope: SurfaceScope.Nature);
            return new DelveCommand(CommandVerb.Help);
        }

        public static RegenerateMode Mode(RegenerateVerdict verdict)
        {
            switch (verdict)
            {
                case RegenerateVerdict.Allowed: return RegenerateMode.SkipDelay;
                case RegenerateVerdict.NotAdmin: return RegenerateMode.Refused;
                default: return RegenerateMode.EvaluateNow;
            }
        }

        public static string Reply(RegenerateVerdict verdict, IReadOnlyList<DelveKind> kinds)
        {
            var what = kinds.Count == Enum.GetValues(typeof(DelveKind)).Length ? "every kind" : string.Join(", ", kinds);
            switch (verdict)
            {
                case RegenerateVerdict.Allowed:
                    return "Delve: " + what + " will regenerate at the next check without waiting for the delay; players near and player buildings still hold sites back (details in the server log).";
                case RegenerateVerdict.CheatOff:
                    return "Delve: " + what + " evaluated now with the usual delays; turn on Cheats / Regenerate Without Waiting to skip them.";
                case RegenerateVerdict.CheatLocked:
                    return "Delve: " + what + " evaluated now with the usual delays; the Mischief gate locks the Regenerate cheat for you.";
                default:
                    return "Delve: only an admin can regenerate sites.";
            }
        }

        public static IReadOnlyList<string> Help() => new[]
        {
            "Delve commands (in chat: /kick <command>, in the F5 console: kick <command>):",
            "  delve-status   delve-regen   " + string.Join("   ", KindWords.Keys.Select(k => "delve-regen-" + k)),
            "  delve-surface   delve-surface-resources   delve-surface-nature   (admins: bring back felled trees, bushes, rocks, ore and pickables in generated zones around you; never near player buildings)",
            "  Regenerating skips the delay only while Cheats / Regenerate Without Waiting is on; players near and player buildings always hold sites back.",
        };
    }
}
