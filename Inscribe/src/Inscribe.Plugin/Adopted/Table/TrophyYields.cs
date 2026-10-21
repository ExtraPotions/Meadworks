// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// What a trophy is worth at the Rune Table (rune-table.md section 3): pure essence for the table's one pool, more the later its creature's
    /// biome (Meadows 2, Black Forest 4, Swamp and the sea 6, Mountains 8, Plains 10, Mistlands 12, Ashlands 14, Deep North 16; lowered
    /// 2026-10-07 by the user's word, so a guaranteed inscription (10 essence per item level) costs about five trophies of
    /// the item's own biome).
    /// Boss trophies are worth the most (user 2026-10-07: "boss trophies should be in there too"): Eikthyr 20 ... the
    /// Fader 80. The Deep North boss (Kall Fimbulbringer) has no trophy item in Valheim 1.0.20. A boss trophy also unlocks a Forsaken power at the altar, so Sacrifice all trophies never takes one
    /// (<see cref="IsBoss"/>): only a press on that trophy does. A trophy the game does not have is simply never offered.
    /// </summary>
    internal static class TrophyYields
    {
        private static readonly Dictionary<string, int> Yields =
            new Dictionary<string, int>
            {
                // Meadows
                ["TrophyBoar"] = 2, ["TrophyNeck"] = 2, ["TrophyDeer"] = 2,
                // Black Forest
                ["TrophyGreydwarf"] = 4, ["TrophyBjorn"] = 4,
                ["TrophyGreydwarfBrute"] = 4, ["TrophyFrostTroll"] = 4,
                ["TrophyGreydwarfShaman"] = 4, ["TrophyGhost"] = 4,
                ["TrophySkeleton"] = 4, ["TrophySkeletonPoison"] = 4,
                // Swamp and the sea
                ["TrophySurtling"] = 6, ["TrophyBlob"] = 6, ["TrophyLeech"] = 6,
                ["TrophyAbomination"] = 6, ["TrophyWraith"] = 6,
                ["TrophyDraugr"] = 6, ["TrophyDraugrElite"] = 6, ["TrophyDraugrFem"] = 6,
                ["TrophySerpent"] = 6,
                // Mountains
                ["TrophyWolf"] = 8, ["TrophyHatchling"] = 8, ["TrophyUlv"] = 8,
                ["TrophyFenring"] = 8, ["TrophyCultist"] = 8, ["TrophySGolem"] = 8,
                // Plains
                ["TrophyLox"] = 10, ["TrophyGoblin"] = 10, ["TrophyGoblinBrute"] = 10,
                ["TrophyGoblinShaman"] = 10, ["TrophyDeathsquito"] = 10,
                ["TrophyGrowth"] = 10, ["TrophyBjornUndead"] = 10,
                // Mistlands
                ["TrophyHare"] = 12, ["TrophySeekerBrute"] = 12, ["TrophyGjall"] = 12,
                ["TrophySeeker"] = 12, ["TrophyTick"] = 12, ["TrophyDvergr"] = 12,
                // Ashlands
                ["TrophyAsksvin"] = 14, ["TrophyVolture"] = 14, ["TrophyMorgen"] = 14,
                ["TrophyCharredArcher"] = 14, ["TrophyCharredMage"] = 14,
                ["TrophyCharredMelee"] = 14, ["TrophyFallenValkyrie"] = 14,
                // Deep North
                ["TrophySeal"] = 16, ["TrophyMoose"] = 16, ["TrophyJotunWarrior"] = 16, ["TrophyJotunWitch"] = 16,
                ["TrophyBarka"] = 16, ["TrophyElaking"] = 16, ["TrophyMole"] = 16, ["TrophyWrithan"] = 16,
                ["TrophyBlob_Frost"] = 16, ["TrophyBlob_Morkhalla"] = 16,
            };

        private static readonly Dictionary<string, int> Bosses =
            new Dictionary<string, int>
            {
                ["TrophyEikthyr"] = 20, ["TrophyTheElder"] = 30, ["TrophyBonemass"] = 40, ["TrophyDragonQueen"] = 50,
                ["TrophyGoblinKing"] = 60, ["TrophySeekerQueen"] = 70, ["TrophyFader"] = 80,
            };

        /// <summary>The essence one of this trophy gives; false for anything else.</summary>
        public static bool TryGet(ItemDrop.ItemData? item, out int amount)
        {
            amount = 0;
            string? prefab = Prefab(item);
            return prefab != null && (Yields.TryGetValue(prefab, out amount) || Bosses.TryGetValue(prefab, out amount));
        }

        /// <summary>A boss trophy: taken one press at a time, never by Sacrifice all trophies.</summary>
        public static bool IsBoss(ItemDrop.ItemData? item)
        {
            string? prefab = Prefab(item);
            return prefab != null && Bosses.ContainsKey(prefab);
        }

        private static string? Prefab(ItemDrop.ItemData? item) => item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;
    }
}
