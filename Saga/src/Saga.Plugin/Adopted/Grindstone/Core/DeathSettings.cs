// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/DeathSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 6: what dying costs every skill. The defaults are the game's own (5% of each level, progress toward the
    /// next level lost). Read at the moment of death, so an edited .cfg applies to the next death. Synced.
    /// </summary>
    public static class DeathSettings
    {
        public const string Section = "6 - Death";

        public static NativeSetting<float> SkillLoss { get; private set; }
        public static NativeSetting<bool> LoseProgress { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            SkillLoss = config.Bind(Section, "Skill Loss On Death", 5f,
                "Percent of every skill's level lost when you die. The game's own value is 5; 0 keeps every level. " +
                "The world's death penalty modifier still scales it, and a world that resets skills on death still does.",
                acceptableValues: Settings.UpTo(100f));
            LoseProgress = config.Bind(Section, "Lose Progress On Death", true,
                "Whether dying also loses the experience gathered toward each skill's next level, as in the game. Off keeps it.");
        }
    }
}
