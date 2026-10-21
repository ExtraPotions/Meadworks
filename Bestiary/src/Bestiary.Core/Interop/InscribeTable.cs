// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (Crafting/CraftingGear.cs, Crafting/CraftingLoot.cs, Crafting/BiomeLevels.cs).
// Changed by expDARE, 2026: Unity-free tables for Inscribe's meadworks.items.classes v1 (design 9.2); biomes as BiomeMask;
// the Rime Giant's bonus is the Ascension Rune as in EliteCreaturesPack 0.9.0 (ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6,
// Crafting/CraftingLoot.cs; EliteCrafting 0.8.0 retired the Consecrated Rune).
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Catalog;
using ExtraPotions.Bestiary.Config;

namespace ExtraPotions.Bestiary.Interop
{
    public readonly struct GearEntry
    {
        public GearEntry(string prefab, string itemClass, int level)
        {
            Prefab = prefab;
            ItemClass = itemClass;
            Level = level;
        }

        public string Prefab { get; }
        public string ItemClass { get; }
        public int Level { get; }
    }

    public readonly struct LootProfile
    {
        public LootProfile(string prefab, string json)
        {
            Prefab = prefab;
            Json = json;
        }

        public string Prefab { get; }

        /// <summary>EliteCrafting <c>SetCreatureLoot</c> text, byte for byte as ECP sends it.</summary>
        public string Json { get; }
    }

    /// <summary>
    /// What Bestiary registers with Inscribe (or EliteCrafting by reflection) on every peer at load: item classes and
    /// levels for its gear, and a loot profile per creature. Ammo, materials and food are left alone. A server's own
    /// Inscribe files override these by prefab name.
    /// </summary>
    public static class InscribeTable
    {
        private const int BlackForest = 2, Mountain = 4, Ocean = 4;

        private static readonly (BiomeMask Biome, int Level)[] Levels =
        {
            (BiomeMask.Meadows, 1), (BiomeMask.BlackForest, 2), (BiomeMask.Swamp, 3), (BiomeMask.Mountain, 4), (BiomeMask.Ocean, 4),
            (BiomeMask.Plains, 5), (BiomeMask.Mistlands, 6), (BiomeMask.AshLands, 7), (BiomeMask.DeepNorth, 8),
        };

        private static readonly (string Weapon, string Class)[] ArsenalClasses =
        {
            ("Dagger", "knife"), ("Sword", "sword_1h"), ("Axe", "axe_1h"), ("Mace", "mace_1h"), ("Spear", "spear"), ("Atgeir", "atgeir"), ("Bow", "bow"),
        };

        public static IReadOnlyList<GearEntry> Gear { get; } = ArsenalClasses
            .Select(a => new GearEntry("ECP_Bone" + a.Weapon, a.Class, BlackForest))
            .Concat(new[]
            {
                new GearEntry("ECP_BoneCrossbow", "crossbow", BlackForest),
                new GearEntry("ECP_ExecutionerGreataxe", "battleaxe", BlackForest),
                new GearEntry("ECP_ShieldKraken", "shield", Ocean),
            }).ToArray();

        /// <summary>EliteCrafting's lowest level among the set biomes (the ocean counts as the Mountain); <paramref name="none"/> when none is set.</summary>
        public static int LowestLevel(BiomeMask biomes, int none)
        {
            var lowest = 0;
            foreach (var (biome, level) in Levels)
                if ((biomes & biome) != 0 && (lowest == 0 || level < lowest))
                    lowest = level;
            return lowest > 0 ? lowest : none;
        }

        /// <summary>The loot profile of every creature, with the giant's and slinger's tiers from their biome settings.</summary>
        public static IEnumerable<LootProfile> CreatureLoot(BiomeMask rimeGiantBiomes, BiomeMask slingerBiomes)
        {
            yield return Boss("ECP_Kraken", Ocean, 3, "serpent", 100);
            yield return Boss("ECP_RimeGiant", LowestLevel(rimeGiantBiomes, Mountain), 3, "ascension", 50);
            yield return Boss("ECP_Headsman", BlackForest, 2, "ascension", 25);
            yield return Entry("ECP_HeadsmanSkeleton", "\"multiplier\": 0");
            yield return Entry("ECP_CryptMimic", "\"rune_multiplier\": 6, \"gear_multiplier\": 8");
            yield return Entry("ECP_GreydwarfSlinger", Tier(LowestLevel(slingerBiomes, BlackForest)));
            foreach (var creature in CreatureCatalog.ArsenalSkeletons)
                yield return Entry(creature.Prefab, Tier(BlackForest));
            yield return Entry("ECP_SkeletonCrossbowman", Tier(BlackForest));
        }

        private static LootProfile Boss(string prefab, int tier, int runes, string bonus, int chance) =>
            Entry(prefab, $"\"boss\": true, {Tier(tier)}, \"rune_rolls\": {runes}, \"gear_rolls\": 1, "
                + $"\"bonus\": [ {{ \"rune\": \"{bonus}\", \"chance\": {chance}, \"amount\": 1 }} ]");

        private static string Tier(int tier) => $"\"tier\": {tier}";

        private static LootProfile Entry(string prefab, string fields) => new LootProfile(prefab, $"{{ \"prefab\": \"{prefab}\", {fields} }}");
    }
}
