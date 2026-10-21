// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/MineFindSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 18: how often a broken chunk of rock, or a broken single-piece rock, hides a find, and the Mine Finds
    /// YAML file (Saga.MineFinds*.yml, registered by <see cref="MineFinds.Register"/>) that says what it
    /// hides per biome or per deposit kind. The chance grows linearly from its value at level 0 to its value at level
    /// 100 of the miner, and is scaled down for chunks of less than 50 health (<see cref="MineFinds.HealthShare"/>).
    /// Synced, like the file.
    /// </summary>
    public static class MineFindSettings
    {
        public const string Section = PickaxeSettings.FindsSection;

        public static NativeSetting<float> ChanceAt0 { get; private set; }
        public static NativeSetting<float> ChanceAt100 { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            ChanceAt0 = config.Bind(Section, "Find Chance At 0", 0.2f,
                "Percent chance that a chunk broken by a level 0 miner hides a find from Saga.MineFinds.yml. Chunks with less than 50 health get a share in proportion (a mud pile's 5-health chunks a tenth). 0 with the next setting at 0 turns finds off.",
                acceptableValues: Settings.UpTo(100f));
            ChanceAt100 = config.Bind(Section, "Find Chance At 100", 1f,
                "Percent chance of a find for a level 100 miner; levels in between are in proportion.",
                acceptableValues: Settings.UpTo(100f));
            MineFinds.Register(config);
        }
    }
}
