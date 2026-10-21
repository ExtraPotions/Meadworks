// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/OldGrowthSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Old growth (section 12, Chopping): more wood from the logs of the biggest trees of each kind. A tree's size is
    /// measured within its own kind's range, 0% for the smallest it grows to and 100% for the largest; the bonus grows
    /// from nothing at the start to all of it at the full size, and linearly with the woodcutter's level. Synced.
    /// </summary>
    public static class OldGrowthSettings
    {
        public const string Section = WoodcuttingSettings.ChoppingSection;

        public static NativeSetting<float> Bonus { get; private set; }
        public static NativeSetting<float> Start { get; private set; }
        public static NativeSetting<float> Full { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            Bonus = config.Bind(Section, "Old Growth Bonus", 100f,
                "Percent more wood from the logs of the biggest trees for a level 100 woodcutter, less for smaller trees and lower levels. It follows the level of whoever breaks the log, or else of whoever felled the tree.",
                acceptableValues: Settings.UpTo(500f));
            Start = config.Bind(Section, "Old Growth Start", 50f,
                "How big a tree must be before its logs give extra wood, in percent of its own kind's size range: 0 is the smallest size that kind of tree grows to, 100 the largest. Trees this size or smaller give no bonus.",
                acceptableValues: Settings.UpTo(100f));
            Full = config.Bind(Section, "Old Growth Full", 100f,
                "Trees this big within their kind, in percent, give the whole bonus, and trees between the start and this size a share of it. If this is not above the start, every tree larger than the start gives the whole bonus.",
                acceptableValues: Settings.UpTo(100f));
        }
    }
}
