// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Delve uses (tokens meadworks_delve_<key>, design section 11). The English
// notice wording follows RestockRegen's message settings (design 4.9: they become the English catalog).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments, params string[] named)
        {
            Key = key;
            Arguments = arguments;
            Named = named.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }

        /// <summary>Named placeholders kept from RestockRegen's history templates (<c>{last}</c>, <c>{lastago}</c>...).</summary>
        public IReadOnlyList<string> Named { get; }
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static class DelveTextKeys
    {
        /// <summary>History uses named source tokens, stored as literal braces in C05's numeric-format grammar.</summary>
        public static string ToCatalog(string key, string text) => IsHistoryTemplate(key)
            ? text.Replace("{", "{{").Replace("}", "}}") : text;

        /// <summary>Restore the source template before ChestHistory substitutes its named tokens.</summary>
        public static string FromCatalog(string key, string text) => IsHistoryTemplate(key)
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, text, Array.Empty<object>()) : text;

        static bool IsHistoryTemplate(string key) => key == HistoryMany || key == HistoryOnce;

        public const string DaysOne = "days_one";
        public const string DaysMany = "days_many";
        public const string HoursOne = "hours_one";
        public const string HoursMany = "hours_many";
        public const string ChestRefill = "chest_refill";
        public const string ChestRefillLeftovers = "chest_refill_leftovers";
        public const string ChestDue = "chest_due";
        public const string HistoryMany = "history_many";
        public const string HistoryOnce = "history_once";
        public const string HistoryYou = "history_you";
        public const string HistoryToday = "history_today";
        public const string HistoryYesterday = "history_yesterday";
        public const string HistoryDaysAgo = "history_days_ago";
        public const string ImportDone = "import_done";
        public const string ServerOnlyIdle = "server_only_idle";

        /// <summary>A block reason in the status report: guard_playernear, guard_held (a client owns and has it loaded),
        /// guard_built, guard_playerbase.</summary>
        public static string Guard(GuardResult guard) => "guard_" + guard.ToString().ToLowerInvariant();

        /// <summary>The dungeon kinds with entrance, inside and due notices, by key stem.</summary>
        public static readonly IReadOnlyList<(DelveKind Kind, string Stem)> SiteKinds = Array.AsReadOnly(new[]
        {
            (DelveKind.MudPiles, "mudpiles"), (DelveKind.FrostCaveCrystals, "crystals"), (DelveKind.BlackCores, "blackcores"),
            (DelveKind.Morkhalla, "morkhalla"), (DelveKind.Hole, "hole"), (DelveKind.Queen, "queen"), (DelveKind.FullReset, "fullreset"),
            (DelveKind.Kall, "kall"),
        });

        /// <summary>The world kinds with a biome entry notice, by biome key.</summary>
        public static readonly IReadOnlyList<(DelveKind Kind, string Key)> BiomeKinds = Array.AsReadOnly(new[]
        {
            (DelveKind.AncientArmor, "biome_mistlands"), (DelveKind.Obsidian, "biome_mountain"),
            (DelveKind.Flametal, "biome_ashlands"), (DelveKind.Ice, "biome_deepnorth"),
        });

        private static readonly Lazy<TextKey[]> all = new Lazy<TextKey[]>(() =>
        {
            var keys = new List<TextKey>
            {
                new TextKey("step_aside_rooms", 1), new TextKey("step_aside_fullreset", 1), new TextKey("step_aside_chests", 1),
                new TextKey("step_aside_resources", 1), new TextKey("step_aside_respawn", 1),
                new TextKey(DaysOne, 0), new TextKey(DaysMany, 1), new TextKey(HoursOne, 0), new TextKey(HoursMany, 1),
                new TextKey(ChestRefill, 1), new TextKey(ChestRefillLeftovers, 1), new TextKey(ChestDue, 0),
                new TextKey(HistoryMany, 0, "last", "lastago", "first", "firstago"), new TextKey(HistoryOnce, 0, "last", "lastago"),
                new TextKey(HistoryYou, 0), new TextKey(HistoryToday, 0), new TextKey(HistoryYesterday, 0), new TextKey(HistoryDaysAgo, 1),
                new TextKey(ImportDone, 2), new TextKey(ServerOnlyIdle, 0),
            };
            foreach (var (_, stem) in SiteKinds)
            {
                keys.Add(new TextKey(stem + "_inside", 1));
                keys.Add(new TextKey(stem + "_entrance", 1));
                keys.Add(new TextKey(stem + "_due", 0));
            }
            foreach (var (_, key) in BiomeKinds) keys.Add(new TextKey(key, 1));
            foreach (GuardResult guard in Enum.GetValues(typeof(GuardResult)))
                if (guard != GuardResult.Clear) keys.Add(new TextKey(Guard(guard), 0));
            return keys.ToArray();
        });

        public static IReadOnlyList<TextKey> All => all.Value;
    }
}
