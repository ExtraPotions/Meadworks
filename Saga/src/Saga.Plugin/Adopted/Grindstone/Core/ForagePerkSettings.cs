// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Core/ForagePerkSettings.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 34: the Foraging perks. Extra yield grows linearly from 0 at level 0; sweep picking starts at its level
    /// and reaches further as the level grows. Synced.
    /// </summary>
    public static class ForagePerkSettings
    {
        public const string Section = ForagingSettings.PerksSection;

        public static NativeSetting<float> ExtraYieldAt100 { get; private set; }
        public static NativeSetting<int> SweepLevel { get; private set; }
        public static NativeSetting<float> SweepRadiusAt100 { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            ExtraYieldAt100 = config.Bind(Section, "Extra Yield Chance At 100", 50f,
                "Percent chance at level 100 that a pick gives one more, growing linearly from 0 at level 0. It replaces the game's own 25% for these picks.",
                acceptableValues: Settings.UpTo(100f));
            SweepLevel = config.Bind(Section, "Sweep Level", 25,
                "From this Foraging level, picking a plant also picks every plant of the same kind around it. 101 turns sweep picking off.",
                acceptableValues: new AcceptableValueRange<int>(0, 101));
            SweepRadiusAt100 = config.Bind(Section, "Sweep Radius At 100", 4f,
                "How far sweep picking reaches, in meters, at level 100; at lower levels it reaches level / 100 of this (1 m at level 25).",
                acceptableValues: Settings.UpTo(10f));
        }
    }
}
