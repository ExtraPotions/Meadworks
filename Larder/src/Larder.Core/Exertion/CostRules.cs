// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/CostRules.cs, FeastMaster/FeastMaster/FeastMasterCore/StaminaCostsMovement.cs,
// FeastMaster/FeastMaster/FeastMasterCore/StaminaCostsActions.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the shared cost rules take the player's state as flags (no Player or
// BaseAI); the per-site multipliers of the movement and action patches became one table (Multiplier) over the
// Exertion settings; the field scaling and restore stay in the plugin; negative multipliers read 0.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Larder.Exertion
{
    /// <summary>The drain sites whose stamina cost Larder scales (design D1).</summary>
    public enum CostSite
    {
        Run,
        Jump,
        Dodge,
        Block,
        Attack,
        Tool,
        Sneak,
        Swim,
        Encumbered,
        FishingPull,
        FishingHooked,
        Harpoon,
    }

    /// <summary>The player's state a cost reads: out of combat, free sneaking, and the site skill's factor (0..1).</summary>
    public readonly struct CostContext
    {
        public CostContext(bool outOfCombat, bool sneakingIsFree, float skillFactor)
        {
            OutOfCombat = outOfCombat;
            SneakingIsFree = sneakingIsFree;
            SkillFactor = skillFactor;
        }

        public bool OutOfCombat { get; }
        public bool SneakingIsFree { get; }

        /// <summary>The level factor of the skill the site's discount uses (Jump, Dodge, Blocking).</summary>
        public float SkillFactor { get; }
    }

    /// <summary>Stamina cost rules (design D1, D2): multipliers, out of combat, free sneaking and the skill discount.</summary>
    public static class CostRules
    {
        /// <summary>Not targeted, not sensed and not attacking.</summary>
        public static bool OutOfCombat(bool targeted, bool sensed, bool attacking) => !targeted && !sensed && !attacking;

        /// <summary>The out of combat multiplier when the player is out of combat, else 1.</summary>
        public static float OutOfCombatFactor(bool outOfCombat, float outOfCombatCost)
        {
            if (outOfCombatCost == 1f)
                return 1f;
            return outOfCombat ? LarderMath.NonNegative(outOfCombatCost) : 1f;
        }

        /// <summary>1 minus the skill discount at the skill's level factor, never below 0.</summary>
        public static float SkillDiscountFactor(float discountPercent, float skillFactor)
        {
            if (discountPercent == 0f)
                return 1f;
            return Math.Max(0f, 1f - discountPercent / 100f * skillFactor);
        }

        /// <summary>
        /// Free sneaking: the setting is on, no enemy is within stealth range, and none has noticed or targets the
        /// player (stealth range is also false while an alerted enemy is close).
        /// </summary>
        public static bool SneakingIsFree(bool enabled, bool inStealthRange, bool sensed, bool targeted) => enabled && !inStealthRange && !sensed && !targeted;

        /// <summary>The multiplier applied to a site's stamina drain.</summary>
        public static float Multiplier(CostSite site, CostContext context, ExertionSettings settings)
        {
            switch (site)
            {
                case CostSite.Run:
                    return LarderMath.NonNegative(settings.RunCost) * OutOfCombatFactor(context.OutOfCombat, settings.OutOfCombatRunCost);
                case CostSite.Jump:
                    return LarderMath.NonNegative(settings.JumpCost) * OutOfCombatFactor(context.OutOfCombat, settings.OutOfCombatJumpCost)
                        * SkillDiscountFactor(settings.SkillDiscount, context.SkillFactor);
                case CostSite.Dodge:
                    return LarderMath.NonNegative(settings.DodgeCost) * OutOfCombatFactor(context.OutOfCombat, settings.OutOfCombatDodgeCost)
                        * SkillDiscountFactor(settings.SkillDiscount, context.SkillFactor);
                case CostSite.Block:
                    return LarderMath.NonNegative(settings.BlockCost) * SkillDiscountFactor(settings.SkillDiscount, context.SkillFactor);
                case CostSite.Sneak:
                    return context.SneakingIsFree && settings.FreeSneakingWithoutEnemies
                        ? 0f
                        : LarderMath.NonNegative(settings.SneakCost) * OutOfCombatFactor(context.OutOfCombat, settings.OutOfCombatSneakCost);
                case CostSite.Attack: return LarderMath.NonNegative(settings.AttackCost);
                case CostSite.Tool: return LarderMath.NonNegative(settings.ToolCost);
                case CostSite.Swim: return LarderMath.NonNegative(settings.SwimCost);
                case CostSite.Encumbered: return LarderMath.NonNegative(settings.EncumberedCost);
                case CostSite.FishingPull: return LarderMath.NonNegative(settings.FishingPullCost);
                case CostSite.FishingHooked: return LarderMath.NonNegative(settings.FishingHookedCost);
                case CostSite.Harpoon: return LarderMath.NonNegative(settings.HarpoonCost);
                default: return 1f;
            }
        }

        /// <summary>
        /// A jump is checked against its reduced cost (the drain field is scaled around the whole jump, so the stamina
        /// check and the drain read the same value); a jump that costs nothing is never refused.
        /// </summary>
        public static bool JumpAllowed(float stamina, float jumpStaminaUsage, float multiplier)
        {
            float cost = jumpStaminaUsage * multiplier;
            return cost <= 0f || stamina > cost;
        }
    }
}
