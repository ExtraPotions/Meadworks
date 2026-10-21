// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Core/HusbandryExperienceSettings.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Section 28: Husbandry experience. Every amount is multiplied by the creature's tier (1 for boars and hens, about
    /// 2.5 for wolves, over 4 for lox, asksvin and moose, from its health) and by the Experience Multiplier. Players within
    /// Keeper Range earn taming, feeding and birth experience; the petter, the killer and the harvester earn theirs.
    /// Synced.
    /// </summary>
    public static class HusbandryExperienceSettings
    {
        public const string Section = HusbandrySettings.ExperienceSection;

        public static HusbandrySetting<float> Multiplier { get; private set; }
        public static HusbandrySetting<float> Taming { get; private set; }
        public static HusbandrySetting<float> Tamed { get; private set; }
        public static HusbandrySetting<float> Discovery { get; private set; }
        public static HusbandrySetting<float> Feeding { get; private set; }
        public static HusbandrySetting<float> Birth { get; private set; }
        public static HusbandrySetting<float> Petting { get; private set; }
        public static HusbandrySetting<float> Butchering { get; private set; }
        public static HusbandrySetting<float> Honey { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindTaming(config);
            BindKeeping(config);
        }

        private static void BindTaming(NativeConfiguration config)
        {
            Multiplier = config.Bind(Section, "Experience Multiplier", 1f,
                "Multiplies all Husbandry experience.", acceptableValues: Settings.UpTo(10f));
            Taming = config.Bind(Section, "Taming Experience", 20f,
                "Experience for a whole taming's progress (0% to 100%), earned bit by bit while you are near, times the tier.",
                acceptableValues: Settings.UpTo(1000f));
            Tamed = config.Bind(Section, "Tamed Experience", 10f,
                "Experience when a creature near you becomes tame, times the tier.", acceptableValues: Settings.UpTo(1000f));
            Discovery = config.Bind(Section, "Discovery Multiplier", 3f,
                "The Tamed Experience is multiplied by this for the first creature of each kind a character tames. 1 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(1f, 10f));
        }

        private static void BindKeeping(NativeConfiguration config)
        {
            Feeding = config.Bind(Section, "Feeding Experience", 1f,
                "Experience each time a tamed animal, or one being tamed, eats near you, times the tier.", acceptableValues: Settings.UpTo(100f));
            Birth = config.Bind(Section, "Birth Experience", 3f,
                "Experience for each birth or laid egg near you, times the tier.", acceptableValues: Settings.UpTo(100f));
            Petting = config.Bind(Section, "Petting Experience", 1f,
                "Experience for petting a tamed animal that is not content yet, times the tier.", acceptableValues: Settings.UpTo(100f));
            Butchering = config.Bind(Section, "Butchering Experience", 5f,
                "Experience for killing a tamed animal, times the tier.", acceptableValues: Settings.UpTo(100f));
            Honey = config.Bind(Section, "Honey Experience", 0.5f,
                "Experience per honey harvested from a beehive (no tier).", acceptableValues: Settings.UpTo(100f));
        }
    }
}
