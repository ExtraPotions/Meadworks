// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/DefenseSettings.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Section 19: the Defense skill's master switch, each player's display preferences and the core perks, and the
    /// order the Defense settings are bound in. Defense has four sections: Defense (this one), Defense Experience,
    /// Defense Milestones and Defense Guard. Each core perk grows linearly from nothing at level 0 to its value at
    /// level 100, on the defending player's own level and client. Gameplay values are synced and lockable; callouts
    /// and the plate are each player's own.
    /// </summary>
    public static class DefenseSettings
    {
        public const string Section = "5 - Defense";
        public const string ExperienceSection = "6 - Defense Experience";
        public const string MilestonesSection = "7 - Defense Milestones";
        public const string GuardSection = "8 - Defense Guard";

        public static NativeSetting<bool> Enabled { get; private set; }
        public static NativeSetting<bool> ShowCallouts { get; private set; }
        public static NativeSetting<float> MaxHealth { get; private set; }
        public static NativeSetting<float> FoodHealth { get; private set; }
        public static NativeSetting<float> DamageReduction { get; private set; }
        public static NativeSetting<float> Regeneration { get; private set; }
        public static NativeSetting<float> RegenerationInterval { get; private set; }
        public static NativeSetting<float> OutOfCombatDelay { get; private set; }
        public static NativeSetting<float> Poise { get; private set; }
        public static NativeSetting<float> ParryWindow { get; private set; }
        public static NativeSetting<float> BlockStamina { get; private set; }
        public static NativeSetting<float> DodgeStamina { get; private set; }

        public static void Initialize(NativeConfiguration config)
        {
            Enabled = config.Bind(Section, "Defense Enabled", true,
                "Turns every Defense perk, milestone, guard perk and Defense experience on or off. Off, fights play exactly as in the game. Levels are kept either way.");
            ShowCallouts = config.Bind(Section, "Show Callouts", true,
                "Shows words like Riposte!, Reflex! and Bash! floating where they happen. Each player's own.", synced: false);
            BindVitality(config);
            BindCombat(config);
            DefenseExperienceSettings.Initialize(config);
            DefenseMilestoneSettings.Initialize(config);
            DefenseGuardSettings.Initialize(config);
        }

        private static void BindVitality(NativeConfiguration config)
        {
            MaxHealth = config.Bind(Section, "Max Health At 100", 25f,
                "Health added to your base health (the game's 25) at level 100, food or no food.",
                acceptableValues: Settings.UpTo(200f));
            FoodHealth = config.Bind(Section, "Food Health At 100", 10f,
                "Percent more health from the food you have eaten, at level 100, counted after cooking stars.",
                acceptableValues: Settings.UpTo(100f));
            DamageReduction = config.Bind(Section, "Damage Reduction At 100", 10f,
                "Percent less damage taken at level 100, from every source, after armor and blocking.",
                acceptableValues: Settings.UpTo(90f));
            Regeneration = config.Bind(Section, "Regeneration At 100", 1f,
                "Percent of your max health healed every Regeneration Interval at level 100, while you are out of combat. The game's health regeneration modifiers (resting, meads) apply to it.",
                acceptableValues: Settings.UpTo(20f));
            RegenerationInterval = config.Bind(Section, "Regeneration Interval", 10f,
                "Seconds between two out-of-combat heals.", acceptableValues: new AcceptableValueRange<float>(1f, 60f));
            OutOfCombatDelay = config.Bind(Section, "Out Of Combat Delay", 10f,
                "Seconds without attacking, blocking or taking damage before out-of-combat regeneration starts.",
                acceptableValues: Settings.UpTo(120f));
        }

        private static void BindCombat(NativeConfiguration config)
        {
            Poise = config.Bind(Section, "Poise At 100", 25f,
                "Percent more stagger damage it takes to stagger you, or to break your guard while blocking, at level 100.",
                acceptableValues: Settings.UpTo(200f));
            ParryWindow = config.Bind(Section, "Parry Window At 100", 0.1f,
                "Seconds added to the game's 0.25 second parry window at level 100, so a block raised a little earlier still parries.",
                acceptableValues: Settings.UpTo(0.5f));
            BlockStamina = config.Bind(Section, "Block Stamina Reduction At 100", 10f,
                "Percent less stamina spent blocking, at level 100.", acceptableValues: Settings.UpTo(100f));
            DodgeStamina = config.Bind(Section, "Dodge Stamina Reduction At 100", 10f,
                "Percent less stamina spent dodging, at level 100, on top of what the game's Dodge skill takes off.",
                acceptableValues: Settings.UpTo(100f));
        }
    }
}
