// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/StaminaRegen.cs, FeastMaster/FeastMaster/FeastMasterCore/RegenBasics.cs,
// FeastMaster/FeastMaster/FeastMasterCore/UpdateStatsPatch.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the SEMan.ModifyStaminaRegen postfix, the regen-basics field scaling,
// the restricted (encumbered, swimming) regen and the swimming delay became pure functions over plain numbers; the
// whole per-second composition in the documented order (design 1.3) is one function, PerSecond, so it can be tested.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Recovery
{
    /// <summary>The Recovery settings the regen rules read (design 4.5); defaults leave the game alone.</summary>
    public sealed class RecoverySettings
    {
        public static RecoverySettings Vanilla => new RecoverySettings();

        public float StaminaRegenMultiplier { get; set; } = 1f;
        public float LowStaminaRegenBonus { get; set; } = 1f;
        public float RegenCurveStrength { get; set; } = 1f;
        public float RegenCurvePivot { get; set; } = 0.5f;
        public float BlockingRegenFactor { get; set; } = StaminaRegenRules.GameBlockingRegenFactor;
    }

    /// <summary>One player's state for one regeneration step (what the game's UpdateStats reads).</summary>
    public struct StaminaRegenInput
    {
        /// <summary>The player's base stamina regen per second (<c>m_staminaRegen</c>).</summary>
        public float StaminaRegen { get; set; }

        /// <summary>The game's low-bar bonus scale (<c>m_staminaRegenTimeMultiplier</c>).</summary>
        public float StaminaRegenTimeMultiplier { get; set; }

        public float Stamina { get; set; }
        public float MaxStamina { get; set; }

        /// <summary>The status effect multiplier (meads, Rested) before Larder's additions.</summary>
        public float StatusEffectMultiplier { get; set; }

        public float VigorPercent { get; set; }
        public float ExtraStaminaPercent { get; set; }
        public float SneakPercent { get; set; }
        public bool Blocking { get; set; }

        /// <summary>The world's (or overridden) stamina regen rate, <c>Game.m_staminaRegenRate</c>.</summary>
        public float WorldRegenRate { get; set; }
    }

    /// <summary>
    /// Stamina regeneration (design C2 to C5). Order (design 1.3, FeastMaster's documented order): base and low-bar
    /// bonus, then the mead and Rested multiplier plus Vigor, extra stamina and sneak bonus, then the curve and the
    /// blocking factor, then the world regen rate.
    /// </summary>
    public static class StaminaRegenRules
    {
        /// <summary>The game's regen factor while blocking.</summary>
        public const float GameBlockingRegenFactor = 0.8f;

        /// <summary>
        /// Max stamina above the player's base and the skill bonus, or with Count Food Stamina Only the active foods'
        /// current stamina.
        /// </summary>
        public static float ExtraStamina(float maxStamina, float baseStamina, float skillBonus, bool countFoodStaminaOnly, float foodStamina)
        {
            return countFoodStaminaOnly ? foodStamina : maxStamina - baseStamina - skillBonus;
        }

        /// <summary>Regen Per Extra Stamina Point (C3): percent added, never negative.</summary>
        public static float ExtraStaminaBonus(float extraStamina, float perPoint)
        {
            if (perPoint == 0f)
                return 0f;
            return (extraStamina > 0f ? extraStamina : 0f) * perPoint;
        }

        /// <summary>Sneak Skill Regen Bonus (C4): crouched and standing still (the game's sneaking is crouching while moving).</summary>
        public static float SneakBonus(float bonusPercent, bool crouching, bool sneakingWhileMoving, float sneakSkillFactor)
        {
            if (bonusPercent == 0f || !crouching || sneakingWhileMoving)
                return 0f;
            return bonusPercent * sneakSkillFactor;
        }

        /// <summary>
        /// The multiplier after Larder's postfix on the game's status effect hook: Vigor, extra stamina and sneak bonus
        /// added (percent), then the curve, then the blocking factor relative to the game's 0.8 (which the game still
        /// applies afterwards).
        /// </summary>
        public static float HookMultiplier(float statusEffectMultiplier, float vigorPercent, float extraStaminaPercent, float sneakPercent,
            float curveFactor, bool blocking, float blockingFactor)
        {
            float multiplier = statusEffectMultiplier + (vigorPercent + extraStaminaPercent + sneakPercent) / 100f;
            multiplier *= curveFactor;
            if (blocking)
                multiplier *= blockingFactor / GameBlockingRegenFactor;
            return multiplier;
        }

        /// <summary>Stamina regenerated per second for one step, in the documented order.</summary>
        public static float PerSecond(StaminaRegenInput input, RecoverySettings settings)
        {
            float baseRegen = input.StaminaRegen * LarderMath.NonNegative(settings.StaminaRegenMultiplier);
            float timeMultiplier = input.StaminaRegenTimeMultiplier * LarderMath.NonNegative(settings.LowStaminaRegenBonus);
            float fraction = input.MaxStamina > 0f ? input.Stamina / input.MaxStamina : 1f;
            float regen = baseRegen + (1f - fraction) * baseRegen * timeMultiplier;
            float curve = RegenCurve.Factor(fraction, settings.RegenCurveStrength, settings.RegenCurvePivot);
            float multiplier = HookMultiplier(input.StatusEffectMultiplier, input.VigorPercent, input.ExtraStaminaPercent, input.SneakPercent,
                curve, input.Blocking, settings.BlockingRegenFactor);
            if (input.Blocking)
                multiplier *= GameBlockingRegenFactor;
            return regen * multiplier * input.WorldRegenRate;
        }

        /// <summary>
        /// The share of normal regen allowed while encumbered or swimming (the game gives none); 0 when the game already
        /// regenerates. Encumbered wins over swimming; swimming waits for the swimming regen delay.
        /// </summary>
        public static float RestrictedFraction(bool encumbered, bool swimming, bool onGround, bool swimDelayElapsed, float encumberedFraction, float swimmingFraction)
        {
            if (encumbered)
                return LarderMath.Clamp01(encumberedFraction);
            if (!swimming || onGround)
                return 0f;
            return swimDelayElapsed ? LarderMath.Clamp01(swimmingFraction) : 0f;
        }

        /// <summary>Whether the swimming regen delay has passed since the last stroke.</summary>
        public static bool SwimDelayElapsed(float now, float lastStroke, float delay) => now - lastStroke >= LarderMath.NonNegative(delay);

        /// <summary>A stamina or eitr regen delay in seconds, never negative.</summary>
        public static float Delay(float configured) => LarderMath.NonNegative(configured);
    }
}
