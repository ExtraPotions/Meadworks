// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/DefenseGuardSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 22: the guard perks, what Defense adds to blocking and to taking hits. Each grows linearly from nothing
    /// at level 0 to its value at level 100, on the defending player's own level; Desperation follows the Damage
    /// Reduction perk. Synced.
    /// </summary>
    public static class DefenseGuardSettings
    {
        public const string Section = DefenseSettings.GuardSection;

        public static NativeSetting<float> ReflexChance { get; private set; }
        public static NativeSetting<float> BashChance { get; private set; }
        public static NativeSetting<float> Thorns { get; private set; }
        public static NativeSetting<float> Adrenaline { get; private set; }
        public static NativeSetting<float> ShieldWear { get; private set; }
        public static NativeSetting<float> Knockback { get; private set; }
        public static NativeSetting<float> DesperationHealth { get; private set; }
        public static NativeSetting<float> DesperationMultiplier { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            ReflexChance = config.Bind(Section, "Reflex Chance At 100", 10f,
                "Percent chance at level 100 that a hit from the front is blocked by your shield although you were not blocking. It costs stamina like a block and never parries.",
                acceptableValues: Settings.UpTo(100f));
            BashChance = config.Bind(Section, "Shield Bash Chance At 100", 15f,
                "Percent chance at level 100 that an ordinary block with a shield staggers the attacker, as a parry does.",
                acceptableValues: Settings.UpTo(100f));
            Thorns = config.Bind(Section, "Thorns At 100", 10f,
                "Percent of the damage your block stopped that goes back to a melee attacker, at level 100.",
                acceptableValues: Settings.UpTo(200f));
            Adrenaline = config.Bind(Section, "Adrenaline Bonus At 100", 25f,
                "Percent more adrenaline from blocks and parries at level 100, and that much less adrenaline lost to hits you did not block.",
                acceptableValues: Settings.UpTo(100f));
            BindWear(config);
        }

        private static void BindWear(NativeConfiguration config)
        {
            ShieldWear = config.Bind(Section, "Shield Wear Reduction At 100", 50f,
                "Percent less durability lost by the shield or weapon you block with, at level 100. 100 means no wear at all.",
                acceptableValues: Settings.UpTo(100f));
            Knockback = config.Bind(Section, "Knockback Reduction At 100", 50f,
                "Percent less knockback from hits you block, at level 100.", acceptableValues: Settings.UpTo(100f));
            DesperationHealth = config.Bind(Section, "Desperation Health", 25f,
                "Below this percent of your max health, Damage Reduction is multiplied by Desperation Multiplier. 0 turns Desperation off.",
                acceptableValues: Settings.UpTo(100f));
            DesperationMultiplier = config.Bind(Section, "Desperation Multiplier", 2f,
                "How much stronger Damage Reduction is while you are desperate.", acceptableValues: new AcceptableValueRange<float>(1f, 5f));
        }
    }
}
