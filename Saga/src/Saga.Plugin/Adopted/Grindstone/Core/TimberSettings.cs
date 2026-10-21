// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/TimberSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Timber! (section 11): how much harder a felled tree is pushed over, and how much less its logs hurt the
    /// woodcutter who felled it, each growing linearly from nothing at level 0 to its value at level 100. Synced.
    /// </summary>
    public static class TimberSettings
    {
        public const string Section = WoodcuttingSettings.FellingSection;

        /// <summary>The largest Fall Push At 100 accepted, in percent: 31 times the game's push at level 100.</summary>
        public const float MaxFallPush = 3000f;

        /// <summary>The largest Log Safety At 100 accepted, in percent: no damage at all.</summary>
        public const float MaxLogSafety = 100f;

        public static NativeSetting<float> FallPushAt100 { get; private set; }
        public static NativeSetting<float> LogSafetyAt100 { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            FallPushAt100 = config.Bind(Section, "Fall Push At 100", 900f,
                "Percent more push a level 100 woodcutter gives a tree as it falls, away from the side it was chopped from. 900 is ten times the game's push, so trees fall where you aim them.",
                acceptableValues: Settings.UpTo(MaxFallPush));
            LogSafetyAt100 = config.Bind(Section, "Log Safety At 100", 100f,
                "Percent less damage a level 100 woodcutter takes from the logs of trees they felled. Other players and creatures take the game's own damage.",
                acceptableValues: Settings.UpTo(MaxLogSafety));
        }
    }
}
