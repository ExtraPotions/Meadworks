// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/PickaxeExperienceSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Pickaxes experience (section 14): the game's own +1 per swing that hits rock, scaled by the hardest rock the swing
    /// hit (its biome, and ore deposits more), plus credits for clean strikes and for the first hit on each kind of ore
    /// deposit. With Experience Per Biome Step and Ore Experience Bonus at 0, Clean Strike and Discovery Experience at 0
    /// and Experience Multiplier 1, a player earns exactly what the game gives. Synced.
    /// </summary>
    public static class PickaxeExperienceSettings
    {
        public const string Section = PickaxeSettings.Section;

        public static NativeSetting<float> Multiplier { get; private set; }
        public static NativeSetting<float> BiomeStep { get; private set; }
        public static NativeSetting<float> OreBonus { get; private set; }
        public static NativeSetting<float> CleanStrike { get; private set; }
        public static NativeSetting<float> Discovery { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindSwing(config);
            BindCredits(config);
        }

        private static void BindSwing(NativeConfiguration config)
        {
            Multiplier = config.Bind(Section, "Experience Multiplier", 1f,
                "Multiplies all Pickaxes experience: swings, clean strikes and discoveries.", acceptableValues: Settings.UpTo(10f));
            BiomeStep = config.Bind(Section, "Experience Per Biome Step", 25f,
                "Percent more experience per swing for each biome step of the rock's biome: Meadows 0, Black Forest 1, Swamp 2, Mountain 3, Plains 4, Mistlands 5, Ashlands and Deep North 6. 25 gives x1.25 in the Black Forest and x2.5 in the Ashlands. Rock too hard for your pickaxe earns no bonus.",
                acceptableValues: Settings.UpTo(200f));
            OreBonus = config.Bind(Section, "Ore Experience Bonus", 50f,
                "Percent more experience per swing when the hardest rock it hit is an ore deposit.", acceptableValues: Settings.UpTo(500f));
        }

        private static void BindCredits(NativeConfiguration config)
        {
            CleanStrike = config.Bind(Section, "Clean Strike Experience", 1f,
                "Pickaxes experience for each clean strike, times the rock's biome and ore scale. 0 turns it off.",
                acceptableValues: Settings.UpTo(100f));
            Discovery = config.Bind(Section, "Discovery Experience", 10f,
                "Pickaxes experience for the first hit on each kind of ore deposit, times its biome and ore scale. Each character discovers each kind once. 0 turns it off.",
                acceptableValues: Settings.UpTo(1000f));
        }
    }
}
