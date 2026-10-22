// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Adapters.Kit
{
    /// <summary>Reads sections 41-44 at use time. The Enabled switches are gated, so General Enabled off stops new spawns.</summary>
    internal static class KitSettings
    {
        private static GatedEntry<bool> bogOn = null!;
        private static GatedEntry<float> bogShare = null!;
        private static GatedEntry<Heightmap.Biome> bogBiomes = null!;
        private static GatedEntry<float> bogDamage = null!;
        private static GatedEntry<float> bogRange = null!;
        private static GatedEntry<float> bogPull = null!;
        private static GatedEntry<float> bogInterval = null!;

        private static GatedEntry<bool> spearOn = null!;
        private static GatedEntry<float> spearShare = null!;
        private static GatedEntry<Heightmap.Biome> spearBiomes = null!;
        private static GatedEntry<float> spearDamage = null!;
        private static GatedEntry<float> spearRange = null!;

        private static GatedEntry<bool> slagOn = null!;
        private static GatedEntry<float> slagRegions = null!;
        private static GatedEntry<float> slagChance = null!;
        private static GatedEntry<float> slagInterval = null!;
        private static GatedEntry<Heightmap.Biome> slagBiomes = null!;
        private static GatedEntry<float> slagHealth = null!;
        private static GatedEntry<int> slagPlates = null!;
        private static GatedEntry<float> slagArmored = null!;
        private static GatedEntry<float> slagFire = null!;

        private static GatedEntry<bool> marrowOn = null!;
        private static GatedEntry<float> marrowPlaces = null!;
        private static GatedEntry<float> marrowChance = null!;
        private static GatedEntry<int> marrowReveal = null!;

        public static bool BogOn => bogOn.Value;
        public static float BogShare => bogShare.Value;
        public static Heightmap.Biome BogBiomes => bogBiomes.Value;
        public static float BogDamage => bogDamage.Value;
        public static float BogRange => bogRange.Value;
        public static float BogPull => bogPull.Value;
        public static float BogInterval => bogInterval.Value;

        public static bool SpearOn => spearOn.Value;
        public static float SpearShare => spearShare.Value;
        public static Heightmap.Biome SpearBiomes => spearBiomes.Value;
        public static float SpearDamage => spearDamage.Value;
        public static float SpearRange => spearRange.Value;

        public static bool SlagOn => slagOn.Value;
        public static float SlagRegions => slagRegions.Value;
        public static float SlagChance => slagChance.Value;
        public static float SlagInterval => slagInterval.Value;
        public static Heightmap.Biome SlagBiomes => slagBiomes.Value;
        public static float SlagHealth => slagHealth.Value;
        public static int SlagPlateCount => slagPlates.Value;
        public static float SlagArmored => slagArmored.Value;
        public static float SlagFire => slagFire.Value;

        public static bool MarrowOn => marrowOn.Value;
        public static float MarrowPlaces => marrowPlaces.Value;
        public static float MarrowChance => marrowChance.Value;
        public static int MarrowReveal => marrowReveal.Value;

        public static void Initialize(NativeConfiguration config)
        {
            bogOn = Bind<bool>(config, BestiarySettings.BogHook, "Enabled");
            bogShare = Bind<float>(config, BestiarySettings.BogHook, "Share");
            bogBiomes = Bind<Heightmap.Biome>(config, BestiarySettings.BogHook, "Biomes");
            bogDamage = Bind<float>(config, BestiarySettings.BogHook, "Hook Damage");
            bogRange = Bind<float>(config, BestiarySettings.BogHook, "Range");
            bogPull = Bind<float>(config, BestiarySettings.BogHook, "Pull");
            bogInterval = Bind<float>(config, BestiarySettings.BogHook, "Interval");

            spearOn = Bind<bool>(config, BestiarySettings.Javelineer, "Enabled");
            spearShare = Bind<float>(config, BestiarySettings.Javelineer, "Share");
            spearBiomes = Bind<Heightmap.Biome>(config, BestiarySettings.Javelineer, "Biomes");
            spearDamage = Bind<float>(config, BestiarySettings.Javelineer, "Javelin Damage");
            spearRange = Bind<float>(config, BestiarySettings.Javelineer, "Throw Range");

            slagOn = Bind<bool>(config, BestiarySettings.SlagCharred, "Enabled");
            slagRegions = Bind<float>(config, BestiarySettings.SlagCharred, "Regions");
            slagChance = Bind<float>(config, BestiarySettings.SlagCharred, "Chance");
            slagInterval = Bind<float>(config, BestiarySettings.SlagCharred, "Interval");
            slagBiomes = Bind<Heightmap.Biome>(config, BestiarySettings.SlagCharred, "Biomes");
            slagHealth = Bind<float>(config, BestiarySettings.SlagCharred, "Health");
            slagPlates = Bind<int>(config, BestiarySettings.SlagCharred, "Plates");
            slagArmored = Bind<float>(config, BestiarySettings.SlagCharred, "Armored Damage");
            slagFire = Bind<float>(config, BestiarySettings.SlagCharred, "Fire Per Plate");

            marrowOn = Bind<bool>(config, BestiarySettings.MarrowLantern, "Enabled");
            marrowPlaces = Bind<float>(config, BestiarySettings.MarrowLantern, "Places");
            marrowChance = Bind<float>(config, BestiarySettings.MarrowLantern, "Chance");
            marrowReveal = Bind<int>(config, BestiarySettings.MarrowLantern, "Reveal");
        }

        private static GatedEntry<T> Bind<T>(NativeConfiguration config, string section, string key) =>
            config.Bind(section, key, default(T)!, key);
    }
}
