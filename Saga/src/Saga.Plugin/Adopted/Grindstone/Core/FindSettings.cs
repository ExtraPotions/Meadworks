// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/FindSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 13: how often a felled tree hides a find, and the Finds YAML file (Saga.Finds*.yml, see
    /// <see cref="FindFile"/>) that says what it hides per biome or per tree. The chance grows linearly from its value
    /// at level 0 to its value at level 100. Synced, like the file.
    /// </summary>
    public static class FindSettings
    {
        public const string Section = WoodcuttingSettings.FindsSection;

        public static NativeSetting<float> ChanceAt0 { get; private set; }
        public static NativeSetting<float> ChanceAt100 { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            ChanceAt0 = config.Bind(Section, "Find Chance At 0", 2f,
                "Percent chance that a tree felled by a level 0 woodcutter hides a find from Saga.Finds.yml. Domino fells count too. 0 with the next setting at 0 turns finds off.",
                acceptableValues: Settings.UpTo(100f));
            ChanceAt100 = config.Bind(Section, "Find Chance At 100", 15f,
                "Percent chance of a find for a level 100 woodcutter; levels in between are in proportion.",
                acceptableValues: Settings.UpTo(100f));
            FindFile.Register(config);
        }
    }
}
