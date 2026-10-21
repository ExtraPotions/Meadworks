// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the biome essences.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Runes;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// The five essences and the inscriptions each guarantees with the Ascension Rune (rune-table.md section 3; user
    /// decision 2026-10-07: fire, frost, lightning and poison are one essence, Elemental, and the attack and draw speeds
    /// that sat with lightning went to Beast). Fixed in code like the rest of the table's tuning: utility inscriptions
    /// (movement, weather, fortune, gathering, skills, item properties) belong to none.
    /// </summary>
    internal static class Essences
    {
        /// <summary>Pure essence one Ascension with an essence costs per item level (1 Meadows ... 8 Deep North).</summary>
        public const int CostPerLevel = 10;

        public static readonly Essence[] All =
        {
            new Essence(0, "beast", "WolfFang", new Color(0.86f, 0.62f, 0.38f),
                "honed_might", "keen_edge", "needlepoint", "keen_eye", "brutal_strikes", "slayer_beasts", "slayer_sea",
                "ambusher", "cruel_opening", "sundering", "deathblow", "nightstalker", "glass_cannon", "wind_siphon",
                "reaper", "endurance", "second_wind", "balanced_grip", "quickened",
                "swift_casting", "swift_string", "quick_windlass", "true_flight", "volley", "twincast", "long_reach",
                "sweeping_arc"),
            new Essence(1, "stone", "Stone", new Color(0.72f, 0.72f, 0.66f),
                "hardened", "padded", "mailed", "riveted", "ironclad", "arrowward", "resolute", "ironroot",
                "quick_recovery", "stalwart", "repelling_guard", "perfect_guard",
                "tireless_guard", "anchored_guard", "keen_guard", "bramblehide", "vigor", "stout_heart", "forsaken_ward",
                "bonebreaker", "staggering_blows", "dazing_blows", "mighty_blows", "press_the_advantage", "heavy_hand",
                "troll_blood", "mending"),
            new Essence(2, "elemental", "SurtlingCore", new Color(0.6f, 0.9f, 1f),
                "emberbrand", "rimebrand", "stormbrand", "venombrand", "primal_fury", "thors_chain", "numbing_blow",
                "bursting_shot", "lingering_wounds", "hamstring", "flameward", "frostward", "stormward", "venomward",
                "elemental_ward", "bulwark_fire", "bulwark_frost", "bulwark_lightning", "bulwark_poison", "ember_skin",
                "quench", "ashen_skin", "emberheart", "coldblood", "winterborn", "purity"),
            new Essence(3, "spirit", "Wisp", new Color(0.8f, 0.74f, 1f),
                "spiritbrand", "spiritward", "wrath", "wellspring", "seidr_flow", "restless_mind",
                "seidr_thrift", "seidr_siphon", "soul_reaper", "seidr_riposte", "runic_ward", "mist_veil",
                "rune_edge"),
            new Essence(4, "grave", "BoneFragments", new Color(0.9f, 0.88f, 0.78f),
                "blood_drinker", "blood_thrift", "blood_price", "slayer_undead", "grave_vigor",
                "grave_command", "shield_mend", "berserkergang"),
            // Meadworks addition (batch 3): the later-biome essences, data in Inscribe.Core's BiomeEssences.
            Biome(5, BiomeEssences.All[0]),
            Biome(6, BiomeEssences.All[1]),
            Biome(7, BiomeEssences.All[2]),
        };

        /// <summary>How many of <see cref="All"/> the table offers now: the first five, plus the biome essences while
        /// <c>2 - Runes / Biome Essences</c> is on (host setting, read live).</summary>
        public static int Offered => BiomeEssences.Offered(Config.ModSettings.BiomeEssences.Value);

        public static bool IsOffered(Essence? essence) => essence != null && essence.Index < Offered;

        private static Essence Biome(int index, BiomeEssence data) =>
            new Essence(index, data.Id, data.IconItem, new Color(data.Color.R, data.Color.G, data.Color.B), System.Linq.Enumerable.ToArray(data.Inscriptions));

        private static readonly Dictionary<string, Essence> ById = Index();

        public static Essence? Get(string? id) => id != null && ById.TryGetValue(id, out Essence essence) ? essence : null;

        /// <summary>What one Ascension with an essence costs on an item of this level.</summary>
        public static int CostFor(int itemLevel) => CostPerLevel * Mathf.Clamp(itemLevel, 1, 8);

        private static Dictionary<string, Essence> Index()
        {
            var index = new Dictionary<string, Essence>();
            foreach (Essence essence in All)
            {
                index[essence.Id] = essence;
            }
            return index;
        }
    }
}
