// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/WoodExperienceSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Woodcutting experience (section 10): the game's own +1 per swing that hits wood, scaled by the hardest wood the
    /// swing could cut, plus credits for felling trees and breaking logs into wood, and a first-fell discovery bonus.
    /// With Tier Scaling off, Small Wood Experience 100, Fell and Split Experience 0 and Experience Multiplier 1, a
    /// player earns exactly what the game gives. Synced.
    /// </summary>
    public static class WoodExperienceSettings
    {
        public const string Section = WoodcuttingSettings.Section;

        public static NativeSetting<float> Multiplier { get; private set; }
        public static NativeSetting<bool> TierScaling { get; private set; }
        public static NativeSetting<float> TierExperience { get; private set; }
        public static NativeSetting<float> SmallWoodHealth { get; private set; }
        public static NativeSetting<float> SmallWoodExperience { get; private set; }
        public static NativeSetting<float> FellExperience { get; private set; }
        public static NativeSetting<float> SplitExperience { get; private set; }
        public static NativeSetting<float> DiscoveryMultiplier { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindSwing(config);
            BindCredits(config);
        }

        private static void BindSwing(NativeConfiguration config)
        {
            Multiplier = config.Bind(Section, "Experience Multiplier", 1f,
                "Multiplies all Woodcutting experience: swings, felled trees and logs broken into wood.", acceptableValues: Settings.UpTo(10f));
            TierScaling = config.Bind(Section, "Tier Scaling", true,
                "Harder wood teaches more: swings, fells and splits earn more for wood that needs a better axe (birch and oak, Yggdrasil).");
            TierExperience = config.Bind(Section, "Tier Experience Per Tool Tier", 50f,
                "Percent more experience for each tool tier the wood needs, while Tier Scaling is on. 50 doubles it for birch and oak and triples it for Yggdrasil shoots. Wood too hard for your axe earns no tier bonus.",
                acceptableValues: Settings.UpTo(500f));
            SmallWoodHealth = config.Bind(Section, "Small Wood Health", 30f,
                "Wood with less health than this (saplings, small trees) counts as small wood. 0 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 200f));
            SmallWoodExperience = config.Bind(Section, "Small Wood Experience", 25f,
                "Percent of the experience a swing earns when the hardest wood it hit is small wood, so saplings are no experience farm.",
                acceptableValues: Settings.UpTo(100f));
        }

        private static void BindCredits(NativeConfiguration config)
        {
            FellExperience = config.Bind(Section, "Fell Experience", 5f,
                "Woodcutting experience for each tree you fell, trees knocked over by your falling logs included. 0 turns it off.",
                acceptableValues: Settings.UpTo(100f));
            SplitExperience = config.Bind(Section, "Split Experience", 2f,
                "Woodcutting experience for each log you break into wood. 0 turns it off.", acceptableValues: Settings.UpTo(100f));
            DiscoveryMultiplier = config.Bind(Section, "Discovery Multiplier", 3f,
                "Multiplies the fell experience the first time a character fells each kind of tree.",
                acceptableValues: new AcceptableValueRange<float>(1f, 10f));
        }
    }
}
