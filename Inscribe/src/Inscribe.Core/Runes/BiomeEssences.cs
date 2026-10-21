// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Rune Table's later-biome essences (batch 3, plan step 6).
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Runes
{
    /// <summary>One later-biome essence: id, the game material whose icon it shows, its text colour and what it steers toward.</summary>
    public sealed class BiomeEssence
    {
        public BiomeEssence(string id, string biome, string iconItem, float r, float g, float b, params string[] inscriptions)
        {
            Id = id;
            Biome = biome;
            IconItem = iconItem;
            Color = (r, g, b);
            Inscriptions = inscriptions;
        }

        public string Id { get; }
        public string Biome { get; }
        public string IconItem { get; }
        public (float R, float G, float B) Color { get; }
        public IReadOnlyList<string> Inscriptions { get; }

        /// <summary>Crucible text key of its name (the table shows <c>$ecf_essence_&lt;id&gt;</c>, filled from this key).</summary>
        public string TextKey => "essence_" + Id;
    }

    /// <summary>
    /// Essences after the original five, shown at the Rune Table while <c>2 - Runes / Biome Essences</c> is on. They work
    /// exactly like the five (one shared pool, 10 essence per item level, Ascension draws only their inscriptions) and steer
    /// toward existing inscriptions none of the five covers, so every steering choice stays distinct. Nothing about them is
    /// saved: the chosen essence lives in the open window, and what it rolls is an ordinary inscription.
    /// </summary>
    public static class BiomeEssences
    {
        /// <summary>The essences the table offered before this list (beast, stone, elemental, spirit, grave).</summary>
        public const int OriginalCount = 5;

        /// <summary>The original five's ids, in table order; their names are text keys too (<c>essence_&lt;id&gt;</c>).</summary>
        public static readonly string[] OriginalIds = { "beast", "stone", "elemental", "spirit", "grave" };

        public static readonly BiomeEssence[] All =
        {
            // Mistlands: moving through the mist and the heights, unseen.
            new BiomeEssence("mist", "Mistlands", "Carapace", 0.56f, 0.78f, 0.86f,
                "mistbane", "huginns_eye", "windstep", "ravens_glide", "spring_heeled", "light_leap", "soft_landing",
                "mountain_goat", "marshstrider", "fleetfoot", "fleet", "stride", "momentum", "evaders_fury", "nimble",
                "elusive", "ghostwalk", "shadowmeld", "soft_tread"),
            // Ashlands: the war of the Charred: fury, finishers, bosses, ammunition and thrown weapons.
            new BiomeEssence("ash", "Ashlands", "CharredBone", 1f, 0.55f, 0.32f,
                "battle_rush", "blood_up", "lone_blade", "steel_rhythm", "godslayer", "unshaken", "skirmisher",
                "easy_draw", "thrifty_quiver", "throwing_grip", "returning", "bifrost_step"),
            // Deep North: lasting out the cold: shelter, warmth, food, meads, breath and swimming.
            new BiomeEssence("frost", "DeepNorth", "FrostCore", 0.82f, 0.93f, 1f,
                "hearthbound", "hearthlight", "hearthglow", "oilskin", "sealegs", "hearty_appetite", "well_fed", "deep_rest",
                "gourmand", "long_brew", "potent_brew", "bottomless_flask", "brewers_haste", "swift_draught", "steady_breath",
                "hardy", "hale", "warding", "purifying", "strong_swimmer"),
        };

        /// <summary>How many essences the table offers: the original five, plus these while the host setting is on.</summary>
        public static int Offered(bool biomeEssences) => OriginalCount + (biomeEssences ? All.Length : 0);

        /// <summary>Whether the essence at this place in the table's row (0 = beast) may steer a roll now.</summary>
        public static bool IsOffered(int index, bool biomeEssences) => index >= 0 && index < Offered(biomeEssences);
    }
}
