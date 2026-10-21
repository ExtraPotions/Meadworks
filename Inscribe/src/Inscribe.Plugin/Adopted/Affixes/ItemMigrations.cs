// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// The chain of format migrations (item-data.md section 8, classes-and-tiers.md section 7), each a pure step over
    /// the parsed state, run in memory on read. The dictionary is untouched until the item is next written for its own
    /// reasons, which then writes the current form. A new step ships with a test holding a frozen v(n) dictionary and
    /// its expected v(n+1) form.
    /// <para>
    /// 1 → 2: format 1 stored a grade 1-7 over seven tiers; format 2 stores a grade on each inscription's own ladder of k
    /// tiers. <c>grade' = clamp(round(grade * k / 7), 1, k)</c> for every roll whose id is defined; values are kept. It
    /// needs the rules (k), so <see cref="Upgrade"/> only marks the state and <see cref="ResolveGrades"/> converts it
    /// each time it is resolved; an orphaned roll keeps its grade until its id is defined again.
    /// </para>
    /// <para>
    /// Retired ids: the health-critical inscriptions (removed in 0.11.0) read as their plain twin, the same effect always
    /// on, and Berserkergang's old values held in its new ladder (<see cref="MergeRetired"/>), also against the rules, after
    /// the grades.
    /// </para>
    /// </summary>
    internal static class ItemMigrations
    {
        /// <summary>The seven tiers every format-1 inscription had.</summary>
        public const int FormatOneTiers = 7;

        public static StateData Upgrade(StateData state)
        {
            string? rarity = RenamedRarity(state.RarityId);
            if (state.Newer || (state.Format >= ItemKeys.CurrentFormat && rarity == state.RarityId))
            {
                return state;
            }
            StateData upgraded = state.Copy();
            upgraded.LegacyGrades = state.Format < 2;
            upgraded.Format = ItemKeys.CurrentFormat;
            upgraded.RarityId = rarity;
            return upgraded;
        }

        /// <summary>The state with format-1 grades converted under these rules; the state itself when there are none.</summary>
        public static StateData ResolveGrades(StateData state, AffixRules rules)
        {
            if (!state.LegacyGrades)
            {
                return state;
            }
            StateData resolved = state.Copy();
            resolved.LegacyGrades = false;
            resolved.Segments = new ItemSegment[state.Segments.Length];
            for (int i = 0; i < state.Segments.Length; i++)
            {
                ItemSegment segment = state.Segments[i];
                AffixDef? def = segment.IsRoll ? rules.Get(segment.Roll.Id) : null;
                resolved.Segments[i] = def == null ? segment : new ItemSegment(Convert(segment.Roll, def.TierCount));
            }
            return resolved;
        }

        /// <summary><c>clamp(round(grade * k / 7), 1, k)</c>, half away from zero.</summary>
        public static int ConvertGrade(int grade, int k) =>
            Math.Max(1, Math.Min(k, (int)Math.Round(grade * (double)k / FormatOneTiers, MidpointRounding.AwayFromZero)));

        private static AffixRoll Convert(AffixRoll roll, int k) =>
            k <= 0 ? roll : new AffixRoll(roll.Id, ConvertGrade(roll.Tier, k), roll.Value);

        /// <summary>Each removed health-critical inscription and the plain twin it reads as.</summary>
        private static readonly Dictionary<string, string> MergedIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["quickened_hc"] = "quickened", ["blood_drinker_hc"] = "blood_drinker", ["stalwart_hc"] = "stalwart",
            ["perfect_guard_hc"] = "perfect_guard", ["mist_veil_hc"] = "mist_veil", ["troll_blood_hc"] = "troll_blood",
            ["second_wind_hc"] = "second_wind", ["seidr_flow_hc"] = "seidr_flow", ["hardened_hc"] = "hardened",
            ["fleetfoot_hc"] = "fleetfoot",
        };

        /// <summary>Kept ids whose ladder shrank when they lost the condition: a stored value is held inside the new row.</summary>
        private static readonly HashSet<string> RescaledIds = new HashSet<string>(StringComparer.Ordinal) { "berserkergang" };

        /// <summary>
        /// The state with every retired roll read as its twin under these rules: the same grade on the twin's ladder,
        /// the value held inside the twin's row for it (a rescaled id the same way, on its own ladder). A roll whose own
        /// id the rules still define, or whose twin they do not, is kept. The state itself when nothing changes.
        /// </summary>
        public static StateData MergeRetired(StateData state, AffixRules rules)
        {
            StateData? merged = null;
            for (int i = 0; i < state.Segments.Length; i++)
            {
                ItemSegment segment = state.Segments[i];
                AffixDef? twin = segment.IsRoll ? TwinOf(segment.Roll.Id, rules) : null;
                AffixRoll moved = twin != null ? AsTwin(segment.Roll, twin) : segment.Roll;
                if (!moved.Equals(segment.Roll))
                {
                    merged ??= CopySegments(state);
                    merged.Segments[i] = new ItemSegment(moved);
                }
            }
            return merged ?? state;
        }

        private static AffixDef? TwinOf(string id, AffixRules rules)
        {
            if (RescaledIds.Contains(id))
            {
                return rules.Get(id);
            }
            return MergedIds.TryGetValue(id, out string twin) && rules.Get(id) == null ? rules.Get(twin) : null;
        }

        private static AffixRoll AsTwin(AffixRoll roll, AffixDef twin)
        {
            int grade = Math.Max(1, Math.Min(twin.TierCount, roll.Tier));
            AffixTierDef? row = twin.TierRow(grade);
            float value = row == null ? roll.Value : Math.Max(row.Min, Math.Min(row.Max, roll.Value));
            return new AffixRoll(twin.Id, grade, value);
        }

        private static StateData CopySegments(StateData state)
        {
            StateData copy = state.Copy();
            copy.Segments = (ItemSegment[])state.Segments.Clone();
            return copy;
        }

        /// <summary>
        /// The six built-in rarities before the runes (2026-10-02) in today's three: Uncommon is Magic, Epic,
        /// Legendary and Mythic are Rare, Common is Normal (never stored). Any other id is kept as it is.
        /// </summary>
        private static string? RenamedRarity(string? id) => id switch
        {
            "common" => null,
            "uncommon" => "magic",
            "epic" or "legendary" or "mythic" => "rare",
            _ => id,
        };
    }
}
