// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Core/HusbandryBreedingSettings.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Section 25: breeding and growing up. Every perk grows linearly from nothing at Husbandry level 0 to its value at
    /// level 100, from the best keeper within Keeper Range of the parent, young animal or egg. Contentment comes from
    /// petting. Synced.
    /// </summary>
    public static class HusbandryBreedingSettings
    {
        public const string Section = HusbandrySettings.BreedingSection;

        public static HusbandrySetting<float> BreedingSpeed { get; private set; }
        public static HusbandrySetting<int> HerdSize { get; private set; }
        public static HusbandrySetting<float> BetterOffspring { get; private set; }
        public static HusbandrySetting<int> MaxOffspringLevel { get; private set; }
        public static HusbandrySetting<float> Twins { get; private set; }
        public static HusbandrySetting<float> GrowthSpeed { get; private set; }
        public static HusbandrySetting<float> ContentDuration { get; private set; }
        public static HusbandrySetting<float> ContentBonus { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindBreeding(config);
            BindOffspring(config);
            BindContent(config);
        }

        private static void BindBreeding(NativeConfiguration config)
        {
            BreedingSpeed = config.Bind(Section, "Breeding Speed At 100", 100f,
                "Percent faster breeding with a level 100 keeper: pregnancy is shorter and fewer breeding checks are skipped. 100 halves the wait.",
                acceptableValues: Settings.UpTo(500f));
            HerdSize = config.Bind("7 - Herd", "Herd Size At 100", 4,
                "More animals of a kind (young included) that may stand within the game's crowding range (10 m, 20 m for lox) before breeding stops, with a level 100 keeper. The game allows 4 to 10 by kind.",
                acceptableValues: new AcceptableValueRange<int>(0, 20));
            GrowthSpeed = config.Bind(Section, "Growth Speed At 100", 100f,
                "Percent faster growing up for tamed young animals and faster hatching for warm eggs, with a level 100 keeper. 100 halves the time.",
                acceptableValues: Settings.UpTo(500f));
        }

        private static void BindOffspring(NativeConfiguration config)
        {
            BetterOffspring = config.Bind(Section, "Better Offspring At 100", 25f,
                "Percent chance, with a level 100 keeper, that a newborn or a laid egg is one star above its parent (the game passes the parent's own level on).",
                acceptableValues: Settings.UpTo(100f));
            MaxOffspringLevel = config.Bind(Section, "Max Offspring Level", 3,
                "The highest game level Better Offspring reaches: 1 is no star, 2 one star, 3 two stars (the most the game spawns). Drops double with each level. With Elite Creatures Reborn installed its own breeding decides stars, and Better Offspring gives one star more, up to one above the stronger parent.",
                acceptableValues: new AcceptableValueRange<int>(1, 10));
            Twins = config.Bind(Section, "Twins At 100", 25f,
                "Percent chance, with a level 100 keeper, that a birth is followed by a second one (a second egg for hens) half a minute later. A twin never has twins of its own.",
                acceptableValues: Settings.UpTo(100f));
        }

        private static void BindContent(NativeConfiguration config)
        {
            ContentDuration = config.Bind(Section, "Content Duration", 600f,
                "Seconds a tamed animal stays content after it is petted. Petting a content animal again earns no experience. 0 turns contentment off.",
                acceptableValues: Settings.UpTo(3600f));
            ContentBonus = config.Bind(Section, "Content Breeding Bonus", 50f,
                "Percent faster breeding while an animal is content, on top of Breeding Speed. Any level can pet.",
                acceptableValues: Settings.UpTo(500f));
        }
    }
}
