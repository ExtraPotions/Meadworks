// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/VeinSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 16: Rich veins (stars on ore deposits, the same on every machine), and the milestones that read stone:
    /// Read the rock (the hover shows the stars) and Echo (a swing on rock pings the nearest ore deposit). The star
    /// chances are per deposit; whatever is left of 100% is no star. Synced.
    /// </summary>
    public static class VeinSettings
    {
        public const string Section = PickaxeSettings.VeinsSection;

        public static NativeSetting<float> Chance1 { get; private set; }
        public static NativeSetting<float> Chance2 { get; private set; }
        public static NativeSetting<float> Chance3 { get; private set; }
        public static NativeSetting<float> BonusPerStar { get; private set; }
        public static NativeSetting<float> ReadTheRockLevel { get; private set; }
        public static NativeSetting<float> EchoLevel { get; private set; }
        public static NativeSetting<float> EchoRadius { get; private set; }
        public static NativeSetting<float> EchoCooldown { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindVeins(config);
            BindMilestones(config);
        }

        private static void BindVeins(NativeConfiguration config)
        {
            Chance1 = config.Bind(Section, "Vein Chance 1 Star", 25f,
                "Percent of ore deposits that are rich veins with 1 star. The three chances together should stay at or below 100; the rest have no star.",
                acceptableValues: Settings.UpTo(100f));
            Chance2 = config.Bind(Section, "Vein Chance 2 Stars", 11f,
                "Percent of ore deposits with 2 stars.", acceptableValues: Settings.UpTo(100f));
            Chance3 = config.Bind(Section, "Vein Chance 3 Stars", 4f,
                "Percent of ore deposits with 3 stars.", acceptableValues: Settings.UpTo(100f));
            BonusPerStar = config.Bind(Section, "Vein Bonus Per Star", 25f,
                "Percent more drop rolls per star for every chunk of a rich vein anyone breaks: whole rolls, plus a chance for the rest. 0 turns the bonus off.",
                acceptableValues: Settings.UpTo(500f));
        }

        private static void BindMilestones(NativeConfiguration config)
        {
            ReadTheRockLevel = config.Bind(Section, "Read The Rock Level", 25f,
                "Pickaxes level from which hovering over an ore deposit shows its stars, and a multi-chunk rock the chunks left. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            EchoLevel = config.Bind(Section, "Echo Level", 50f,
                "Pickaxes level from which a swing that hits rock pings the nearest ore deposit not yet found by the Wishbone. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            EchoRadius = config.Bind(Section, "Echo Radius", 40f,
                "How far the Echo reaches, in meters.", acceptableValues: new AcceptableValueRange<float>(5f, 100f));
            EchoCooldown = config.Bind(Section, "Echo Cooldown", 10f,
                "Seconds before a player's next Echo.", acceptableValues: Settings.UpTo(600f));
        }
    }
}
