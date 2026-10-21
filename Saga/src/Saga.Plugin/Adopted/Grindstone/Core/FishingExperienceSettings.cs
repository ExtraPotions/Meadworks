// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/FishingExperienceSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Fishing experience (section 50). The game raises Fishing once a second while you reel an empty line and twice a
    /// second while a fish is on it; both are scaled here, and landing a fish earns a credit by its species and size,
    /// with more for the first catch of a species and of each of its sizes. With Empty Reel and Fight Experience at 100,
    /// the three credits at 0 and Experience Multiplier 1, a player earns exactly what the game gives. Synced.
    /// </summary>
    public static class FishingExperienceSettings
    {
        public const string Section = FishingSettings.Section;

        public static NativeSetting<float> Multiplier { get; private set; }
        public static NativeSetting<float> EmptyReel { get; private set; }
        public static NativeSetting<float> FightReel { get; private set; }
        public static NativeSetting<float> Catch { get; private set; }
        public static NativeSetting<float> SizeBonus { get; private set; }
        public static NativeSetting<float> Discovery { get; private set; }
        public static NativeSetting<float> NewSize { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindReeling(config);
            BindCredits(config);
        }

        private static void BindReeling(NativeConfiguration config)
        {
            Multiplier = config.Bind(Section, "Experience Multiplier", 1f,
                "Multiplies all Fishing experience: reeling, catches and discoveries.", acceptableValues: Settings.UpTo(10f));
            EmptyReel = config.Bind(Section, "Empty Reel Experience", 0f,
                "Percent of the game's reeling experience earned while no fish is on the line. The game gives 100, which makes casting and reeling an empty line the fastest way to train; 0 stops that.",
                acceptableValues: Settings.UpTo(100f));
            FightReel = config.Bind(Section, "Fight Experience", 100f,
                "Percent of the game's reeling experience earned while a fish is on the line.", acceptableValues: Settings.UpTo(500f));
        }

        private static void BindCredits(NativeConfiguration config)
        {
            Catch = config.Bind(Section, "Catch Experience", 10f,
                "Fishing experience for each fish you land, times its species (the square root of how hard it pulls against a Perch: a Pike x1.3, a Northern salmon x2.6) and its size. 0 turns it off.",
                acceptableValues: Settings.UpTo(1000f));
            SizeBonus = config.Bind(Section, "Size Experience Bonus", 50f,
                "Percent more catch experience for each level of the fish above 1: 50 gives x3 for a level 5 fish.",
                acceptableValues: Settings.UpTo(500f));
            Discovery = config.Bind(Section, "Discovery Experience", 30f,
                "Fishing experience for the first catch of each species, times its species. Each character discovers each species once. 0 turns it off.",
                acceptableValues: Settings.UpTo(1000f));
            NewSize = config.Bind(Section, "New Size Experience", 10f,
                "Fishing experience for the first catch of a species you know at a size (level) you have not landed before, times its species. 0 turns it off.",
                acceptableValues: Settings.UpTo(1000f));
        }
    }
}
