// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Core/ForagingSettings.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 33: the Foraging module's master switch, each player's own callouts, and Foraging experience;
    /// section 34 (<see cref="ForagePerkSettings"/>) holds the perks. What counts as forage is the Forage YAML file
    /// (Saga.Forage*.yml, <see cref="ForageFile"/>). Gameplay values are synced and lockable.
    /// </summary>
    public static class ForagingSettings
    {
        public const string Section = "19 - Foraging";
        public const string PerksSection = "20 - Forage Perks";

        public static NativeSetting<bool> Enabled { get; private set; }
        public static NativeSetting<bool> ShowCallouts { get; private set; }
        public static NativeSetting<float> ExperiencePerPick { get; private set; }
        public static NativeSetting<float> BiomeStep { get; private set; }
        public static NativeSetting<float> DiscoveryMultiplier { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindSwitches(config);
            BindExperience(config);
            ForagePerkSettings.Initialize(config);
            ForageFile.Register(config);
        }

        private static void BindSwitches(NativeConfiguration config)
        {
            Enabled = config.Bind(Section, "Foraging Enabled", true,
                "Turns every Foraging feature on or off: experience, extra yield, sweep picking. Off, wild plants are picked exactly as in the game (the game trains Farming with them). Levels are kept either way.");
            ShowCallouts = config.Bind(Section, "Show Callouts", true,
                "Shows words like Discovered Thistle! floating above plants you pick.", synced: false);
        }

        private static void BindExperience(NativeConfiguration config)
        {
            ExperiencePerPick = config.Bind(Section, "Experience Per Pick", 3f,
                "Foraging experience for one pick in the Meadows, times the item's own factor in the Forage file. The game gives 1 per pick, to Farming.",
                acceptableValues: Settings.UpTo(50f));
            BiomeStep = config.Bind(Section, "Experience Per Biome Step", 25f,
                "Percent more experience per pick for each biome step of the plant's biome: Meadows 0, Black Forest 1, Swamp 2, Mountain 3, Plains 4, Mistlands 5, Ashlands and Deep North 6.",
                acceptableValues: Settings.UpTo(200f));
            DiscoveryMultiplier = config.Bind(Section, "Discovery Multiplier", 3f,
                "The first pick of each kind of forage earns this many times the experience. Each character discovers each kind once. 1 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(1f, 20f));
        }
    }
}
