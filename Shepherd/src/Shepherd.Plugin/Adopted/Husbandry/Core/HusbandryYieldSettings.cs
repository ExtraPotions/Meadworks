// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Core/HusbandryYieldSettings.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 9da28eb58adbe065be1e4baef779a448acd38e0f
// Honey production settings adapted from OpenKeep/OpenKeep/src/Homestead/HiveSettings.cs
// at 3bb778330f23645ccddb42fbc0c6ffab72c842b6; native/off defaults retained.
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Section 26: what animals give. Butchering follows the killer's level; produce the best keeper's within Keeper
    /// Range; extra honey the harvester's. Each grows linearly from nothing at Husbandry level 0 to its value at level 100.
    /// Synced.
    /// </summary>
    public static class HusbandryYieldSettings
    {
        public const string Section = HusbandrySettings.YieldSection;

        public static HusbandrySetting<float> ButcherYield { get; private set; }
        public static HusbandrySetting<float> ProduceChance { get; private set; }
        public static HusbandrySetting<float> ProduceInterval { get; private set; }
        public static HusbandrySetting<float> ExtraHoney { get; private set; }
        public static HusbandrySetting<float> HoneyPerDay { get; private set; }
        public static HusbandrySetting<bool> HoneyPerPlayerOnline { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindButcher(config);
            BindLiving(config);
        }

        private static void BindButcher(NativeConfiguration config)
        {
            ButcherYield = config.Bind(Section, "Butcher Yield At 100", 50f,
                "Percent more drops from a tamed animal killed by a level 100 player (meat, hides, feathers; never trophies). A fraction is a chance of one more.",
                acceptableValues: Settings.UpTo(500f));
        }

        private static void BindLiving(NativeConfiguration config)
        {
            HoneyPerDay = config.Bind(Section, "Honey Per Day", 0f,
                "Honey each hive produces per native game day. 0 keeps its native rate. Capacity and harvest bonuses are unchanged.",
                acceptableValues: Settings.UpTo(1000f));
            HoneyPerPlayerOnline = config.Bind(Section, "Honey Per Player Online", false,
                "Each hive produces one honey per online player per native game day. Overrides Honey Per Day.");
            ProduceChance = config.Bind(Section, "Produce Chance At 100", 50f,
                "Percent chance, each Produce Interval, that a fed tamed animal with a level 100 keeper near drops one of its own materials without being killed: feathers from hens, leather scraps from boars, pelts from wolves and lox, hides from moose. Meat and trophies never. 0 turns it off.",
                acceptableValues: Settings.UpTo(100f));
            ProduceInterval = config.Bind(Section, "Produce Interval", 1200f,
                "Seconds between an animal's produce rolls.", acceptableValues: new AcceptableValueRange<float>(60f, 86400f));
            ExtraHoney = config.Bind(Section, "Extra Honey At 100", 50f,
                "Percent chance per honey that a level 100 player harvesting a beehive gets one more. 0 turns it off.",
                acceptableValues: Settings.UpTo(100f));
        }
    }
}
