// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Core/HusbandrySettings.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Section 23: the Husbandry skill's master switch, the keeper range, the Animal lore level and each player's
    /// callout preference, and the order the Husbandry feature settings are bound in. The features share six sections:
    /// Husbandry (this one), Taming, Breeding, Animal Yield, Companions and Husbandry Experience. Gameplay values are
    /// synced and lockable; callouts are each player's own.
    /// </summary>
    public static class HusbandrySettings
    {
        public const string Section = "4 - Husbandry";
        public const string TamingSection = "5 - Taming";
        public const string BreedingSection = "6 - Breeding";
        public const string YieldSection = "8 - Yield";
        public const string CompanionSection = "9 - Companions";
        public const string ExperienceSection = "10 - Experience";

        public static HusbandrySetting<bool> Enabled { get; private set; }
        public static HusbandrySetting<float> KeeperRange { get; private set; }
        public static HusbandrySetting<float> LoreLevel { get; private set; }
        public static HusbandrySetting<bool> ShowCallouts { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            Enabled = config.Bind("1 - General", "Husbandry Enabled", true,
                "Turns every Husbandry feature on or off: taming, breeding, yield, companions, the feeder's eating, animal lore and experience. Off, animals behave exactly as in the game. Levels are kept either way.");
            KeeperRange = config.Bind(Section, "Keeper Range", 30f,
                "Meters. An animal is tended by the best Husbandry level among the players this close to it: that level drives its fed time, breeding, twins, better offspring, growth and produce, and players this close earn experience from it. Taming speed uses the game's own taming range (60 m) instead.",
                acceptableValues: new AcceptableValueRange<float>(5f, 100f), synced: true, tags: new object[] { new ConfigDependency(() => ShepherdSettings.Enabled.Value && Enabled.Value, () => "Requires Shepherd: Enabled and Husbandry Enabled.") });
            LoreLevel = config.Bind(Section, "Animal Lore Level", 20f,
                "Husbandry level from which hovering an animal or an egg shows its timers: fed time left, taming time left, love and pregnancy, herd room, contentment, growing up and hatching. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f), synced: true, tags: new object[] { new ConfigDependency(() => ShepherdSettings.Enabled.Value && Enabled.Value, () => "Requires Shepherd: Enabled and Husbandry Enabled.") });
            ShowCallouts = config.Bind(Section, "Show Callouts", true,
                "Shows words like Twins! and Strong offspring! floating above animals near you.", synced: false, acceptableValues: null, tags: new object[] { new ConfigDependency(() => ShepherdSettings.Enabled.Value && Enabled.Value, () => "Requires Shepherd: Enabled and Husbandry Enabled.") });
            HusbandryTamingSettings.Initialize(config);
            HusbandryBreedingSettings.Initialize(config);
            HusbandryYieldSettings.Initialize(config);
            HusbandryCompanionSettings.Initialize(config);
            HusbandryExperienceSettings.Initialize(config);
        }
    }
}
