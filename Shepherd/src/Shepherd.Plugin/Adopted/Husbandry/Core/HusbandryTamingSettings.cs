// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Core/HusbandryTamingSettings.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Section 24: taming. Taming speed grows linearly from nothing at Husbandry level 0 to its value at level 100, from
    /// the best keeper within the game's taming range; the fed-time bonus from the best keeper within Keeper Range when the
    /// animal eats. Calm is a milestone: from Calm Level, a creature you have started taming neither fears nor attacks you.
    /// Taming Levels can make a creature need a keeper of some level before taming progresses. Synced.
    /// </summary>
    public static class HusbandryTamingSettings
    {
        public const string Section = HusbandrySettings.TamingSection;

        public static HusbandrySetting<float> TamingSpeed { get; private set; }
        public static HusbandrySetting<float> FedDuration { get; private set; }
        public static HusbandrySetting<float> CalmLevel { get; private set; }
        public static HusbandrySetting<float> CalmBreakTime { get; private set; }
        public static HusbandrySetting<string> TamingLevels { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindSpeed(config);
            BindCalm(config);
        }

        private static void BindSpeed(NativeConfiguration config)
        {
            TamingSpeed = config.Bind(Section, "Taming Speed At 100", 100f,
                "Percent faster taming with a level 100 keeper within the creature's taming range (the game's 60 m). 100 tames in half the time. It stacks with the game's own taming boost.",
                acceptableValues: Settings.UpTo(500f));
            FedDuration = config.Bind(Section, "Fed Duration At 100", 100f,
                "Percent longer an animal stays fed after eating, with a level 100 keeper within Keeper Range when it eats. 100 doubles it (the game's 10 minutes become 20). Animals breed, heal and tame only while fed.",
                acceptableValues: Settings.UpTo(500f));
            TamingLevels = config.Bind(Section, "Taming Levels", "",
                "Creatures that need a keeper of some Husbandry level before taming makes progress: creature prefab names with a level, comma-separated, e.g. Lox:30, Asksvin:50, Moose:60. The keeper must stand within the game's taming range (60 m). Empty: anyone tames anything, as in the game.");
        }

        private static void BindCalm(NativeConfiguration config)
        {
            CalmLevel = config.Bind(Section, "Calm Level", 50f,
                "Husbandry level from which a creature you have started taming (tameness above 0%) neither flees from nor attacks you, and you no longer frighten it, so taming goes on while you stand beside it. Only you: players below the level still scare it. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            CalmBreakTime = config.Bind(Section, "Calm Break Time", 120f,
                "Seconds a creature stays wary of a calm player after that player hurt it: it defends itself as usual until then.",
                acceptableValues: Settings.UpTo(3600f));
        }
    }
}
