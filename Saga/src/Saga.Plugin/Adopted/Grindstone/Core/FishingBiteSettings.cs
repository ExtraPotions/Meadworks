// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Core/FishingBiteSettings.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 52: how often fish bite, and what the angler can read. The game gives each fish a 10% chance to go for a
    /// float within 50 m every time it picks a new place to swim; that chance grows with the angler's level, at dawn and
    /// dusk, in rain and near chum floating in the water. The senses are milestones: the nibbling species, then its
    /// size, then what is in reach. Synced.
    /// </summary>
    public static class FishingBiteSettings
    {
        public const string Section = FishingSettings.BitesSection;

        public static NativeSetting<float> BiteChanceAt100 { get; private set; }
        public static NativeSetting<float> DawnDuskBonus { get; private set; }
        public static NativeSetting<float> RainBonus { get; private set; }
        public static NativeSetting<string> ChumItems { get; private set; }
        public static NativeSetting<float> ChumBonus { get; private set; }
        public static NativeSetting<float> ChumRadius { get; private set; }
        public static NativeSetting<float> ChumDuration { get; private set; }
        public static NativeSetting<float> SpeciesSenseLevel { get; private set; }
        public static NativeSetting<float> SizeSenseLevel { get; private set; }
        public static NativeSetting<float> WaterSenseLevel { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindBites(config);
            BindChum(config);
            BindSenses(config);
        }

        private static void BindBites(NativeConfiguration config)
        {
            BiteChanceAt100 = config.Bind(Section, "Bite Chance At 100", 100f,
                "Percent more bites for a level 100 angler: 100 doubles the game's chance that a fish goes for your float.",
                acceptableValues: Settings.UpTo(500f));
            DawnDuskBonus = config.Bind(Section, "Dawn And Dusk Bite Bonus", 50f,
                "Percent more bites at the height of dawn and dusk, fading in and out around them (the hours fish jump most in the game).",
                acceptableValues: Settings.UpTo(500f));
            RainBonus = config.Bind(Section, "Rain Bite Bonus", 25f,
                "Percent more bites in wet weather.", acceptableValues: Settings.UpTo(500f));
        }

        private static void BindChum(NativeConfiguration config)
        {
            ChumItems = config.Bind(Section, "Chum Items", "Entrails, Bloodbag",
                "Item prefab names, comma-separated, that are chum: dropped in the water they draw fish to any float near them, then dissolve. Empty turns chum off.");
            ChumBonus = config.Bind(Section, "Chum Bite Bonus", 100f,
                "Percent more bites for a float with chum floating near it.", acceptableValues: Settings.UpTo(500f));
            ChumRadius = config.Bind(Section, "Chum Radius", 12f,
                "How close chum must float to a float, in meters.", acceptableValues: new AcceptableValueRange<float>(1f, 50f));
            ChumDuration = config.Bind(Section, "Chum Duration", 60f,
                "Seconds a piece of chum lasts in the water before it dissolves (checked every 10 seconds).",
                acceptableValues: new AcceptableValueRange<float>(10f, 3600f));
        }

        private static void BindSenses(NativeConfiguration config)
        {
            SpeciesSenseLevel = config.Bind(Section, "Species Sense Level", 25f,
                "Fishing level from which a nibble names the fish, and a cast that lands tells the fishing conditions. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            SizeSenseLevel = config.Bind(Section, "Size Sense Level", 50f,
                "Fishing level from which a nibble also tells the fish's size (level), so you can let a small one go. Looking at a fish in the water shows it too.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            WaterSenseLevel = config.Bind(Section, "Water Sense Level", 75f,
                "Fishing level from which a cast that lands tells which fish in reach take your bait, and whether something big lurks.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
        }
    }
}
