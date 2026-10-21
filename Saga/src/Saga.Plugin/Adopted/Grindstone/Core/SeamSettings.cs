// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/SeamSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 15: seams and clean strikes on multi-chunk rocks, and the Unbroken milestone. A seam opens on the
    /// swinging player's own client with a chance, and stays open for a window, both growing linearly from their value
    /// at level 0 to their value at level 100 of the miner's own Pickaxes level. Synced.
    /// </summary>
    public static class SeamSettings
    {
        public const string Section = PickaxeSettings.SeamsSection;

        public static NativeSetting<float> ChanceAt0 { get; private set; }
        public static NativeSetting<float> ChanceAt100 { get; private set; }
        public static NativeSetting<float> WindowAt0 { get; private set; }
        public static NativeSetting<float> WindowAt100 { get; private set; }
        public static NativeSetting<float> CleanStrikeDamage { get; private set; }
        public static NativeSetting<float> UnbrokenLevel { get; private set; }
        public static NativeSetting<float> UnbrokenBonus { get; private set; }
        public static NativeSetting<int> UnbrokenMaxLinks { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            BindSeams(config);
            BindUnbroken(config);
        }

        private static void BindSeams(NativeConfiguration config)
        {
            ChanceAt0 = config.Bind(Section, "Seam Chance At 0", 10f,
                "Percent of swings on a multi-chunk rock that open a seam on another chunk, for a level 0 miner. 0 with the next setting at 0 turns seams off.",
                acceptableValues: Settings.UpTo(100f));
            ChanceAt100 = config.Bind(Section, "Seam Chance At 100", 40f,
                "Percent chance of a seam for a level 100 miner; levels in between are in proportion.", acceptableValues: Settings.UpTo(100f));
            WindowAt0 = config.Bind(Section, "Seam Window At 0", 2.6f,
                "Seconds a seam stays open for a level 0 miner. Hit the glowing chunk before it closes for a clean strike.",
                acceptableValues: new AcceptableValueRange<float>(0.5f, 30f));
            WindowAt100 = config.Bind(Section, "Seam Window At 100", 5.6f,
                "Seconds a seam stays open for a level 100 miner; levels in between are in proportion.",
                acceptableValues: new AcceptableValueRange<float>(0.5f, 30f));
            CleanStrikeDamage = config.Bind(Section, "Clean Strike Damage", 2f,
                "Damage multiplier of a clean strike, the hit on an open seam. A clean strike on an ore deposit also rolls its chunk's drops once more.",
                acceptableValues: new AcceptableValueRange<float>(1f, 10f));
        }

        private static void BindUnbroken(NativeConfiguration config)
        {
            UnbrokenLevel = config.Bind(Section, "Unbroken Level", 100f,
                "Pickaxes level that unlocks Unbroken: clean strikes in a row hit harder. 0 gives it to everyone; above 100 turns it off.",
                acceptableValues: new AcceptableValueRange<float>(0f, 101f));
            UnbrokenBonus = config.Bind(Section, "Unbroken Bonus Per Link", 20f,
                "Percent added to the clean strike damage multiplier for each clean strike after the first in a chain.",
                acceptableValues: Settings.UpTo(200f));
            UnbrokenMaxLinks = config.Bind(Section, "Unbroken Max Links", 5,
                "How many links of a chain add the bonus; further clean strikes keep the last multiplier.",
                acceptableValues: new AcceptableValueRange<int>(1, 50));
        }
    }
}
