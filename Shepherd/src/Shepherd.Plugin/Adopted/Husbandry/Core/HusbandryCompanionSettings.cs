// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Core/HusbandryCompanionSettings.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Section 27: companions and the animal feeder. Pack leader grows linearly from nothing at Husbandry level 0 to its
    /// value at level 100, from the level of the player a tamed creature follows. The feeder is a piece a player of Feeder
    /// Level can build; hungry tamed animals and animals being tamed eat from it. Synced.
    /// </summary>
    public static class HusbandryCompanionSettings
    {
        public const string Section = HusbandrySettings.CompanionSection;

        public static HusbandrySetting<float> PackDamage { get; private set; }
        public static HusbandrySetting<float> PackToughness { get; private set; }
        public static HusbandrySetting<float> FeederLevel { get; private set; }
        public static HusbandrySetting<float> FeederRange { get; private set; }
        public static HusbandrySetting<string> FeederRecipe { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindPack(config);
            BindFeeder(config);
        }

        private static void BindPack(NativeConfiguration config)
        {
            PackDamage = config.Bind(Section, "Pack Damage At 100", 50f,
                "Percent more damage dealt by a tamed creature that follows a level 100 player (wolves, which the game lets you command).",
                acceptableValues: Settings.UpTo(500f));
            PackToughness = config.Bind(Section, "Pack Toughness At 100", 33f,
                "Percent less damage taken by a tamed creature that follows a level 100 player.",
                acceptableValues: Settings.UpTo(90f));
        }

        private static void BindFeeder(NativeConfiguration config)
        {
            FeederLevel = config.Bind("3 - Troughs", "Build Level", 0f,
                "Husbandry level from which a player can build the Animal Feeder (hammer, Misc). 0 lets everyone; above 100 hides it from the hammer. Feeders already built keep working.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            FeederRange = config.Bind("2 - Feeding", "Trough Range", 15f,
                "Meters. A hungry tamed animal, or one being tamed, walks to a feeder this close that holds food it eats, and eats one item from it.",
                acceptableValues: new AcceptableValueRange<float>(2f, 40f));
            FeederRecipe = config.Bind("3 - Troughs", "Feed Barrel Recipe", "Wood:10, LeatherScraps:4",
                "What the Animal Feeder costs: item prefab names with amounts, comma-separated. Built at a workbench. Unknown items are skipped; an empty or unusable recipe falls back to Wood:10.");
        }
    }
}
