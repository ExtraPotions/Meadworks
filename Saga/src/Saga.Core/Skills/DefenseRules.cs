// Saga - ExtraPotions Meadworks progression.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.14.1, GPL-3.0-or-later.
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// GrindstoneSkills/GrindstoneSkills/Defense/Experience/DefenseXp.cs, Defense/Perks/Vitality.cs, Defense/Core/Reductions.cs,
// Defense/Perks/StaminaPerks.cs, Defense/Perks/ParryWindow.cs, Defense/Perks/Poise.cs, Defense/Perks/Recovery.cs,
// Defense/Guard/Desperation.cs, Defense/Milestones/Hardened.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Saga.Skills; Unity-free; the formulas only, as pure functions of a settings
// snapshot (DefenseValues, defaults from Saga's settings table) and the level, health and hit facts the plugin reads
// on the defender's client; the experience cooldown is an explicit clock; milestones by enum.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Saga.Config;

namespace ExtraPotions.Saga.Skills
{
    public enum DefenseMilestone { Riposte, ShieldWall, Hardened, LastStand }

    /// <summary>The Defense settings in effect (design 4.4); <see cref="Defaults"/> reads the settings table.</summary>
    public sealed class DefenseValues
    {
        public float MaxHealthAt100, FoodHealthAt100, DamageReductionAt100, RegenerationAt100, PoiseAt100, ParryWindowAt100;
        public float BlockStaminaReductionAt100, DodgeStaminaReductionAt100;
        public float ExperienceMultiplier, BlockExperience, ParryMultiplier, WeaponBlockShare, HitTakenExperience;
        public float HitSizeDamage, MinHitSize, MaxHitSize, FirstBlockBonus;
        public int RiposteLevel, ShieldWallLevel, HardenedLevel, LastStandLevel, HardenedMaxStacks;
        public float HardenedPerStack, ShieldWallReduction, DesperationHealth, DesperationMultiplier;

        public static DefenseValues Defaults()
        {
            return new DefenseValues
            {
                MaxHealthAt100 = SagaSettings.Defense.MaxHealthAt100.Default,
                FoodHealthAt100 = SagaSettings.Defense.FoodHealthAt100.Default,
                DamageReductionAt100 = SagaSettings.Defense.DamageReductionAt100.Default,
                RegenerationAt100 = SagaSettings.Defense.RegenerationAt100.Default,
                PoiseAt100 = SagaSettings.Defense.PoiseAt100.Default,
                ParryWindowAt100 = SagaSettings.Defense.ParryWindowAt100.Default,
                BlockStaminaReductionAt100 = SagaSettings.Defense.BlockStaminaReductionAt100.Default,
                DodgeStaminaReductionAt100 = SagaSettings.Defense.DodgeStaminaReductionAt100.Default,
                ExperienceMultiplier = SagaSettings.Defense.ExperienceMultiplier.Default,
                BlockExperience = SagaSettings.Defense.BlockExperience.Default,
                ParryMultiplier = SagaSettings.Defense.ParryMultiplier.Default,
                WeaponBlockShare = SagaSettings.Defense.WeaponBlockShare.Default,
                HitTakenExperience = SagaSettings.Defense.HitTakenExperience.Default,
                HitSizeDamage = SagaSettings.Defense.HitSizeDamage.Default,
                MinHitSize = SagaSettings.Defense.MinHitSize.Default,
                MaxHitSize = SagaSettings.Defense.MaxHitSize.Default,
                FirstBlockBonus = SagaSettings.Defense.FirstBlockBonus.Default,
                RiposteLevel = SagaSettings.Defense.RiposteLevel.Default,
                ShieldWallLevel = SagaSettings.Defense.ShieldWallLevel.Default,
                HardenedLevel = SagaSettings.Defense.HardenedLevel.Default,
                LastStandLevel = SagaSettings.Defense.LastStandLevel.Default,
                HardenedMaxStacks = SagaSettings.Defense.HardenedMaxStacks.Default,
                HardenedPerStack = SagaSettings.Defense.HardenedPerStack.Default,
                ShieldWallReduction = SagaSettings.Defense.ShieldWallReduction.Default,
                DesperationHealth = SagaSettings.Defense.DesperationHealth.Default,
                DesperationMultiplier = SagaSettings.Defense.DesperationMultiplier.Default,
            };
        }
    }

    /// <summary>One hit on the defender, when it is over: blocked or not, parried, with a shield, first block of that kind of creature.</summary>
    public readonly struct DefenseHit
    {
        public DefenseHit(bool blocked, bool parried, bool withShield, bool firstOfKind, float taken, float rawDamage)
        {
            Blocked = blocked;
            Parried = parried;
            WithShield = withShield;
            FirstOfKind = firstOfKind;
            Taken = taken;
            RawDamage = rawDamage;
        }

        public bool Blocked { get; }
        public bool Parried { get; }
        public bool WithShield { get; }
        public bool FirstOfKind { get; }

        /// <summary>Health the hit took after every reduction.</summary>
        public float Taken { get; }

        /// <summary>The hit's damage as it arrived, before armor and blocks.</summary>
        public float RawDamage { get; }
    }

    /// <summary>After a hit earned experience, the next can only after the cooldown (seconds of game time).</summary>
    public sealed class ExperienceClock
    {
        private float last = float.NegativeInfinity;

        public bool TryCredit(float now, float cooldown)
        {
            if (now - last < Math.Max(0f, cooldown)) return false;
            last = now;
            return true;
        }

        public void Reset() => last = float.NegativeInfinity;
    }

    public static class DefenseRules
    {
        /// <summary>The game's parry window after raising a block, in seconds.</summary>
        public const float GameParryWindow = 0.25f;

        private const float MaxEach = 0.9f;

        /// <summary>Flat max health added at a level (Vitality).</summary>
        public static float BonusHealth(DefenseValues v, float level) => Math.Max(0f, v.MaxHealthAt100) * SkillCurve.Factor(level);

        /// <summary>Health added on top of what food gives (Vitality: food health is the total minus the base health).</summary>
        public static float FoodHealth(DefenseValues v, float level, float foodHealth) => Math.Max(0f, foodHealth) * SkillCurve.Share(v.FoodHealthAt100, level);

        /// <summary>The core Damage Reduction, multiplied while desperate, capped at 90 percent (Reductions.Toughness).</summary>
        public static float Toughness(DefenseValues v, float level, bool desperate)
        {
            var share = SkillCurve.Share(v.DamageReductionAt100, level);
            if (desperate) share *= Math.Max(1f, v.DesperationMultiplier);
            return Math.Min(MaxEach, share);
        }

        /// <summary>The share of damage still taken: core, Hardened stacks and Shield Wall multiplied, each capped at 90 percent.</summary>
        public static float Keep(DefenseValues v, float level, bool desperate, int hardenedStacks, bool sheltered)
        {
            var keep = 1f - Toughness(v, level, desperate);
            keep *= 1f - Math.Min(MaxEach, Math.Max(0, hardenedStacks) * Math.Max(0f, v.HardenedPerStack) / 100f);
            keep *= 1f - Math.Min(MaxEach, sheltered ? Math.Min(1f, Math.Max(0f, v.ShieldWallReduction / 100f)) : 0f);
            return keep;
        }

        /// <summary>Desperation holds below Desperation Health percent of max health, alive, while there is a reduction to multiply.</summary>
        public static bool Desperate(DefenseValues v, float level, float health, float maxHealth)
        {
            var threshold = v.DesperationHealth / 100f;
            if (threshold <= 0f || v.DesperationMultiplier <= 1f || SkillCurve.Share(v.DamageReductionAt100, level) <= 0f) return false;
            return maxHealth > 0f && health > 0f && health / maxHealth < threshold;
        }

        /// <summary>Multiplier of a positive block stamina cost.</summary>
        public static float BlockStaminaFactor(DefenseValues v, float level) => 1f - Math.Min(1f, SkillCurve.Share(v.BlockStaminaReductionAt100, level));

        /// <summary>Multiplier of the dodge stamina cost after the game's Dodge skill.</summary>
        public static float DodgeStaminaFactor(DefenseValues v, float level) => 1f - Math.Min(1f, SkillCurve.Share(v.DodgeStaminaReductionAt100, level));

        /// <summary>The parry window in seconds: the game's plus Parry Window At 100 at the level.</summary>
        public static float ParryWindow(DefenseValues v, float level) => GameParryWindow + Math.Max(0f, v.ParryWindowAt100) * SkillCurve.Factor(level);

        /// <summary>Multiplier of the stagger threshold.</summary>
        public static float PoiseFactor(DefenseValues v, float level) => 1f + SkillCurve.Share(v.PoiseAt100, level);

        /// <summary>Health healed this interval out of combat; the game's health regeneration multiplier scales it.</summary>
        public static float RegenerationHeal(DefenseValues v, float level, float maxHealth, float regenMultiplier, bool outOfCombat) =>
            outOfCombat ? Math.Max(0f, maxHealth) * SkillCurve.Share(v.RegenerationAt100, level) * Math.Max(0f, regenMultiplier) : 0f;

        public static int MilestoneLevel(DefenseValues v, DefenseMilestone milestone)
        {
            switch (milestone)
            {
                case DefenseMilestone.Riposte: return v.RiposteLevel;
                case DefenseMilestone.ShieldWall: return v.ShieldWallLevel;
                case DefenseMilestone.Hardened: return v.HardenedLevel;
                case DefenseMilestone.LastStand: return v.LastStandLevel;
                default: throw new ArgumentException("Unknown milestone.", nameof(milestone));
            }
        }

        public static bool Unlocked(DefenseValues v, DefenseMilestone milestone, float level) => SkillCurve.Reached(level, MilestoneLevel(v, milestone));

        /// <summary>Hardened stacks after one more hurting hit: one more, up to the maximum.</summary>
        public static int NextHardenedStacks(DefenseValues v, int current) => Math.Min(Math.Max(0, current) + 1, Math.Max(1, v.HardenedMaxStacks));

        /// <summary>The size of a hit: √(damage ÷ Hit Size Damage) between Min and Max Hit Size.</summary>
        public static float HitSize(DefenseValues v, float rawDamage)
        {
            var reference = Math.Max(1f, v.HitSizeDamage);
            var min = Math.Max(0f, v.MinHitSize);
            var max = Math.Max(min, v.MaxHitSize);
            return Math.Min(max, Math.Max(min, (float)Math.Sqrt(Math.Max(0f, rawDamage) / reference)));
        }

        /// <summary>
        /// Experience for one hit (before the cooldown): a block earns Block Experience, times Parry Multiplier for a parry,
        /// times Weapon Block Share without a shield, times First Block Bonus the first time against that kind of creature;
        /// an unblocked hit that hurt earns Hit Taken Experience; each times the hit size and the Experience Multiplier.
        /// </summary>
        public static float Experience(DefenseValues v, DefenseHit hit)
        {
            float amount;
            if (hit.Blocked)
            {
                amount = Math.Max(0f, v.BlockExperience);
                if (hit.Parried) amount *= Math.Max(0f, v.ParryMultiplier);
                if (!hit.WithShield) amount *= Math.Min(1f, Math.Max(0f, v.WeaponBlockShare / 100f));
                if (amount > 0f && hit.FirstOfKind) amount *= Math.Max(1f, v.FirstBlockBonus);
            }
            else
            {
                amount = hit.Taken > 0f ? Math.Max(0f, v.HitTakenExperience) : 0f;
            }
            return amount * HitSize(v, hit.RawDamage) * Math.Max(0f, v.ExperienceMultiplier);
        }
    }
}
