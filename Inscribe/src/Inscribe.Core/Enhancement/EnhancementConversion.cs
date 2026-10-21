// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the one-time conversion of the removed enhancement system (Inscribe 0.2.0) into Honing steps on
// inscriptions, Honing Runes and Pure Essence. The ItemEnhancements key and prefab names are facts (strings), not code.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Inscribe.Items;

namespace ExtraPotions.Inscribe.Enhancement
{
    /// <summary>One tier row an inscription can hold on this item: its grade (1 = weakest) and value range (already scaled).</summary>
    public readonly struct TierRange
    {
        public TierRange(int grade, float min, float max, int decimals)
        {
            Grade = grade;
            Min = Math.Min(min, max);
            Max = Math.Max(min, max);
            Decimals = Math.Max(0, Math.Min(decimals, 6));
        }

        public int Grade { get; }
        public float Min { get; }
        public float Max { get; }
        public int Decimals { get; }
    }

    /// <summary>
    /// An inscription as the conversion sees it: its current row and value, and the stronger rows the Honing Rune's rules
    /// open on this item in order (empty for flags, dormant inscriptions or one already at its best open tier).
    /// </summary>
    public sealed class ConvertibleInscription
    {
        public ConvertibleInscription(TierRange current, float value, IReadOnlyList<TierRange>? higher)
        {
            Current = current;
            Value = value;
            Higher = higher ?? Array.Empty<TierRange>();
        }

        public TierRange Current { get; }
        public float Value { get; }
        public IReadOnlyList<TierRange> Higher { get; }
    }

    /// <summary>A raised inscription: its index on the item, its new grade and value.</summary>
    public readonly struct RaisedInscription
    {
        public RaisedInscription(int index, int grade, float value)
        {
            Index = index;
            Grade = grade;
            Value = value;
        }

        public int Index { get; }
        public int Grade { get; }
        public float Value { get; }
    }

    /// <summary>What one enhanced item converts into.</summary>
    public sealed class ItemConversion
    {
        internal ItemConversion(int level, int steps, IReadOnlyList<RaisedInscription> raised, int tiersRaised, int honingRunes)
        {
            Level = level;
            Steps = steps;
            Raised = raised;
            TiersRaised = tiersRaised;
            HoningRunes = honingRunes;
        }

        public int Level { get; }
        public int Steps { get; }

        /// <summary>The final state of every inscription that rose, one entry per inscription.</summary>
        public IReadOnlyList<RaisedInscription> Raised { get; }

        /// <summary>Tiers gained across all inscriptions (Steps minus HoningRunes).</summary>
        public int TiersRaised { get; }

        /// <summary>Steps no inscription could take, given as Honing Runes.</summary>
        public int HoningRunes { get; }

        /// <summary>The marker value written to <see cref="ItemKeys.EnhancementConverted"/>.</summary>
        public string Marker => "v1;level=" + Level.ToString(CultureInfo.InvariantCulture) + ";steps=" + Steps.ToString(CultureInfo.InvariantCulture)
            + ";raised=" + TiersRaised.ToString(CultureInfo.InvariantCulture) + ";runes=" + HoningRunes.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>What the enhancement scrolls of one inventory convert into.</summary>
    public readonly struct ScrollConversion
    {
        public ScrollConversion(int honingRunes, int essence)
        {
            HoningRunes = honingRunes;
            Essence = essence;
        }

        public int HoningRunes { get; }
        public int Essence { get; }
        public bool IsEmpty => HoningRunes == 0 && Essence == 0;
    }

    /// <summary>
    /// The removed enhancement system's conversion (spec <c>.ai/BATCH-NEXT-ITEM-UPGRADES-20261019.md</c>, "Conversion
    /// (implemented)"). Value is counted in ninths of a Honing Rune through the scroll fusion ratio of three to one: a tier 1
    /// scroll is 1 ninth, tier 2 is 3, tier 3 is 9 (one Honing Rune) and tier 4 is 27. Everything here is deterministic,
    /// so the same item converts the same way on any machine, and an item carrying the marker never converts again.
    /// </summary>
    public static class EnhancementConversion
    {
        public const int MaxLevel = 20;
        public const int ScrollTiers = 4;
        public const int NinthsPerHoningRune = 9;

        /// <summary>Pure Essence paid per remaining ninth (one tier 1 scroll).</summary>
        public const int EssencePerNinth = 2;

        public const string MarkerVersion = "v1";

        /// <summary>The scroll prefabs of the removed system, tier 1 to 4 (ItemEnhancements' names; still registered so saves load).</summary>
        public static IReadOnlyList<string> ScrollPrefabs { get; } = Array.AsReadOnly(new[] { "ScrollEnhance_Tier1", "ScrollEnhance_Tier2", "ScrollEnhance_Tier3", "ScrollEnhance_Tier4" });

        /// <summary>The scroll tier of a prefab name (1 to 4), or 0 when it is not an enhancement scroll.</summary>
        public static int ScrollTier(string? prefab)
        {
            for (int i = 0; i < ScrollPrefabs.Count; i++)
                if (string.Equals(prefab, ScrollPrefabs[i], StringComparison.Ordinal)) return i + 1;
            return 0;
        }

        /// <summary>Ninths of a Honing Rune one scroll of this tier is worth: 1, 3, 9, 27; 0 outside 1 to 4.</summary>
        public static int Ninths(int tier) => tier < 1 || tier > ScrollTiers ? 0 : (int)Math.Pow(3, tier - 1);

        /// <summary>The scroll tier an attempt at this target level used: +1 to +5 tier 1, +6 to +10 tier 2, +11 to +15 tier 3, +16 to +20 tier 4.</summary>
        public static int TierForLevel(int level) => level <= 5 ? 1 : level <= 10 ? 2 : level <= 15 ? 3 : 4;

        /// <summary>The stored level, clamped 0 to 20; 0 when the data, the key or a number is missing.</summary>
        public static int ReadLevel(IReadOnlyDictionary<string, string>? data)
        {
            if (data == null || !data.TryGetValue(ItemKeys.EnhancementLevel, out var text)) return 0;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ? Math.Min(Math.Max(level, 0), MaxLevel) : 0;
        }

        public static bool IsConverted(IReadOnlyDictionary<string, string>? data) =>
            data != null && data.TryGetValue(ItemKeys.EnhancementConverted, out var marker) && !string.IsNullOrEmpty(marker);

        /// <summary>
        /// Whether a pass may run now: the module is active and the session's authority is ready. A joining client is
        /// "active" before the server's settings and rules arrive (the product gate allows an unknown server), and a
        /// pass then would raise tiers by this machine's rules and could run on a server that turns Inscribe off.
        /// </summary>
        public static bool MayRun(bool moduleActive, bool authorityReady) => moduleActive && authorityReady;

        /// <summary>A level above 0 that has not been converted yet.</summary>
        public static bool NeedsConversion(IReadOnlyDictionary<string, string>? data) => ReadLevel(data) > 0 && !IsConverted(data);

        /// <summary>Honing steps a level is worth: each level the scroll its attempt used, summed in ninths, rounded up, at least 1.</summary>
        public static int Steps(int level)
        {
            level = Math.Min(Math.Max(level, 0), MaxLevel);
            if (level == 0) return 0;
            int ninths = 0;
            for (int n = 1; n <= level; n++) ninths += Ninths(TierForLevel(n));
            return Math.Max(1, (ninths + NinthsPerHoningRune - 1) / NinthsPerHoningRune);
        }

        /// <summary>
        /// The conversion of an item at <paramref name="level"/>: its steps raise the inscriptions one tier at a time,
        /// round-robin in display order (null entries are inscriptions that cannot rise); what no inscription can take
        /// becomes Honing Runes. Each value keeps its place inside the tier, rounded to the tier's decimals, never lower.
        /// </summary>
        public static ItemConversion PlanItem(int level, IReadOnlyList<ConvertibleInscription?>? inscriptions)
        {
            level = Math.Min(Math.Max(level, 0), MaxLevel);
            int steps = Steps(level);
            var list = inscriptions ?? Array.Empty<ConvertibleInscription?>();
            var taken = new int[list.Count];
            int left = steps;
            bool progressed = true;
            while (left > 0 && progressed)
            {
                progressed = false;
                for (int i = 0; i < list.Count && left > 0; i++)
                {
                    if (list[i] == null || taken[i] >= list[i]!.Higher.Count) continue;
                    taken[i]++;
                    left--;
                    progressed = true;
                }
            }
            var raised = new List<RaisedInscription>();
            for (int i = 0; i < list.Count; i++)
            {
                if (taken[i] == 0) continue;
                var inscription = list[i]!;
                var row = inscription.Higher[taken[i] - 1];
                raised.Add(new RaisedInscription(i, row.Grade, Carry(inscription.Value, inscription.Current, row)));
            }
            return new ItemConversion(level, steps, raised, steps - left, left);
        }

        /// <summary>The value's place inside <paramref name="from"/> carried into <paramref name="to"/>; never below the old value, never above the new row.</summary>
        public static float Carry(float value, TierRange from, TierRange to)
        {
            double place = from.Max > from.Min ? (value - from.Min) / (double)(from.Max - from.Min) : 1.0;
            place = Math.Min(1.0, Math.Max(0.0, place));
            double carried = Math.Round(to.Min + place * (to.Max - to.Min), to.Decimals, MidpointRounding.AwayFromZero);
            carried = Math.Max(carried, value);
            return (float)Math.Min(Math.Max(carried, to.Min), to.Max);
        }

        /// <summary>
        /// The scrolls of one inventory, by tier (counts[0] is tier 1): every whole nine ninths becomes a Honing Rune, each
        /// remaining ninth <see cref="EssencePerNinth"/> Pure Essence. Counts below zero count as none.
        /// </summary>
        public static ScrollConversion PlanScrolls(IReadOnlyList<int>? counts)
        {
            long ninths = 0;
            for (int tier = 1; counts != null && tier <= Math.Min(counts.Count, ScrollTiers); tier++)
                ninths += (long)Math.Max(0, counts[tier - 1]) * Ninths(tier);
            long runes = ninths / NinthsPerHoningRune;
            long essence = (ninths % NinthsPerHoningRune) * EssencePerNinth;
            return new ScrollConversion((int)Math.Min(runes, int.MaxValue), (int)essence);
        }

        /// <summary>Writes the marker; every other key, the level and the crafter included, stays as it was.</summary>
        public static void Mark(IDictionary<string, string> data, ItemConversion conversion)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (conversion == null) throw new ArgumentNullException(nameof(conversion));
            data[ItemKeys.EnhancementConverted] = conversion.Marker;
        }

        /// <summary>Reads a marker back: level, steps, tiers raised and runes; null when absent or not a v1 marker.</summary>
        public static (int Level, int Steps, int Raised, int Runes)? ReadMarker(IReadOnlyDictionary<string, string>? data)
        {
            if (data == null || !data.TryGetValue(ItemKeys.EnhancementConverted, out var text) || string.IsNullOrEmpty(text)) return null;
            var parts = text.Split(';');
            if (parts.Length == 0 || parts[0] != MarkerVersion) return null;
            var values = parts.Skip(1).Select(p => p.Split('=')).Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1, StringComparer.Ordinal);
            int Get(string key) => values.TryGetValue(key, out var v) ? v : -1;
            return (Get("level"), Get("steps"), Get("raised"), Get("runes"));
        }
    }
}
