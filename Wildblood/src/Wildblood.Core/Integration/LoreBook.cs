// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the portable rules of the lore window and the Compendium pages (design H4/WB14).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ExtraPotions.Wildblood.Integration
{
    public enum LoreTab { Mutations, Modifiers, Aspects, Difficulty }

    /// <summary>What the <c>wildblood</c> console command does with its arguments. Spawn, Purge and Karma are the Creature Commands
    /// cheat; Effects is an admin tool; the rest are read-only or the player's own theft records.</summary>
    public enum LoreRoute { Help, Lore, Reference, Tier, Inspect, TheftStatus, TheftRecover, Spawn, Purge, Effects, Karma }

    /// <summary>Why a console verb may or may not run now (first failing check wins).</summary>
    public enum CommandGate { Allowed, AuthorityNotReady, NotAdmin, CheatOff }

    public static class LoreBook
    {
        /// <summary>The console root Wildblood registers when the name is free.</summary>
        public const string Command = "wildblood";

        /// <summary>Upper bound of one entry body; rule prose stays short and a hostile value cannot grow a page.</summary>
        public const int MaxBody = 1024;

        public const int MaxTitle = 96;

        public static IReadOnlyList<LoreTab> Tabs { get; } = Array.AsReadOnly(new[] { LoreTab.Mutations, LoreTab.Modifiers, LoreTab.Aspects, LoreTab.Difficulty });

        public static string TabKey(LoreTab tab) => "lore_tab_" + tab.ToString().ToLowerInvariant();

        public static int Pages(int count, int pageSize) => Math.Max(1, (Math.Max(0, count) + pageSize - 1) / pageSize);

        public static int ClampPage(int page, int count, int pageSize) => Math.Min(Math.Max(0, page), Pages(count, pageSize) - 1);

        /// <summary>Entry indices shown on <paramref name="page"/> (already clamped by the caller or here).</summary>
        public static IEnumerable<int> PageIndices(int page, int count, int pageSize)
        {
            int start = ClampPage(page, count, pageSize) * pageSize;
            return Enumerable.Range(start, Math.Max(0, Math.Min(pageSize, count - start)));
        }

        /// <summary>Plain display text: control characters become spaces (newlines stay), rich-text brackets and the game's
        /// <c>$</c> localization marker are replaced by look-alikes, and the result is at most <paramref name="max"/> characters.</summary>
        public static string Literal(string? text, int max)
        {
            if (string.IsNullOrEmpty(text) || max <= 0) return "";
            var builder = new StringBuilder(Math.Min(text!.Length, max));
            foreach (char c in text)
            {
                builder.Append(c switch
                {
                    '\n' => '\n',
                    '<' => '‹',
                    '>' => '›',
                    '$' => '＄',
                    _ => char.IsControl(c) ? ' ' : c,
                });
                if (builder.Length > max) break;
            }
            if (builder.Length > max) { builder.Length = max - 1; builder.Append('…'); }
            return builder.ToString();
        }

        /// <summary>`wildblood inspect` on a ready authority: open to every player while the host's Players May Inspect is on, else
        /// only to a server-confirmed admin (Crucible C02). It only reads synced traits, so it is not a cheat and the
        /// Players May Use cheat list does not apply.</summary>
        public static bool InspectAllowed(bool authorityReady, bool isAdmin, bool playersMayInspect) => authorityReady && (isAdmin || playersMayInspect);

        /// <summary>The verbs behind the Creature Commands cheat (spawn, purge, karma); they change the world.</summary>
        public static bool IsCheat(LoreRoute route) => route == LoreRoute.Spawn || route == LoreRoute.Purge || route == LoreRoute.Karma;

        /// <summary>Whether <paramref name="route"/> may run: the verbs that change the world or play effects need a ready authority and a
        /// server-confirmed admin (Crucible C02), and the cheat verbs also need the Creature Commands cheat on (a locked server evaluates it off).
        /// Read-only verbs and the player's own theft records are open.</summary>
        public static CommandGate Gate(LoreRoute route, bool authorityReady, bool isAdmin, bool creatureCommandsOn)
        {
            if (!IsCheat(route) && route != LoreRoute.Effects) return CommandGate.Allowed;
            if (!authorityReady) return CommandGate.AuthorityNotReady;
            if (!isAdmin) return CommandGate.NotAdmin;
            if (IsCheat(route) && !creatureCommandsOn) return CommandGate.CheatOff;
            return CommandGate.Allowed;
        }

        /// <summary><paramref name="args"/> as the game passes them: the command name first. The read-only lore, reference, tier and inspect,
        /// the player's own theft status and explicit recovery query, and the admin verbs spawn, purge, effects and karma (gated by
        /// <see cref="Gate"/>) route; anything else is help. There is no tier override verb.</summary>
        public static LoreRoute Route(IReadOnlyList<string>? args)
        {
            if (args == null || args.Count < 2) return LoreRoute.Help;
            bool Is(int index, string verb) => string.Equals(args[index], verb, StringComparison.OrdinalIgnoreCase);
            // Verbs that take free arguments: spawn <prefab> <stars> [words...], effects <text>, purge [radius], karma [value].
            if (Is(1, "spawn")) return LoreRoute.Spawn;
            if (Is(1, "effects")) return LoreRoute.Effects;
            if (Is(1, "purge")) return args.Count <= 3 ? LoreRoute.Purge : LoreRoute.Help;
            if (Is(1, "karma")) return args.Count <= 3 ? LoreRoute.Karma : LoreRoute.Help;
            if (args.Count > 3) return LoreRoute.Help;
            if (args.Count == 3)
            {
                if (!Is(1, "theft")) return LoreRoute.Help;
                return Is(2, "status") ? LoreRoute.TheftStatus : Is(2, "recover") ? LoreRoute.TheftRecover : LoreRoute.Help;
            }
            if (Is(1, "lore")) return LoreRoute.Lore;
            if (Is(1, "reference")) return LoreRoute.Reference;
            if (Is(1, "tier")) return LoreRoute.Tier;
            if (Is(1, "inspect")) return LoreRoute.Inspect;
            if (Is(1, "theft")) return LoreRoute.TheftStatus;
            return LoreRoute.Help;
        }
    }

    /// <summary>Entries Wildblood put into a list another owner rebuilds (the game's Compendium). Identity is by
    /// reference: a foreign page with the same topic is never claimed, and removal touches only owned items.</summary>
    public sealed class OwnedEntries<T> where T : class
    {
        sealed class Identity : IEqualityComparer<T>
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        readonly HashSet<T> owned = new HashSet<T>(new Identity());

        public int Count => owned.Count;

        public bool Owns(T? item) => item != null && owned.Contains(item);

        public void Append(IList<T> list, IEnumerable<T> items)
        {
            foreach (T item in items)
            {
                list.Add(item);
                owned.Add(item);
            }
        }

        public int RemoveFrom(IList<T> list)
        {
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
                if (owned.Contains(list[i])) { list.RemoveAt(i); removed++; }
            owned.Clear();
            return removed;
        }

        /// <summary>The owner cleared and rebuilt its list: the previous items are gone with it.</summary>
        public void Forget() => owned.Clear();
    }
}
