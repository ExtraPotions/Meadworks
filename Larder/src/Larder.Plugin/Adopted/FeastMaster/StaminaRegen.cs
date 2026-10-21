// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/StaminaRegen.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// The regeneration multiplier rules, applied through the game's single hook for status effect multipliers:
    /// Player.UpdateStats calls SEMan.ModifyStaminaRegen(ref multiplier) and multiplies its regen by the result.
    /// Vigor, extra stamina and the sneak bonus are added to the multiplier; the bar-level curve then scales it.
    /// Also installed for <see cref="RestrictedStaminaRegen"/> alone, which only needs the last postfix: it keeps the
    /// multiplier the game's UpdateStats got (<see cref="StaminaCapture"/>), so the hook is not asked twice per step.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyStaminaRegen))]
    public static class StaminaRegenMultiplierPatch
    {
        public static bool Prepare() => ChangedRules.StaminaRegenRules || ChangedRules.RestrictedRegen || SafeStamina.Enabled;

        public static bool Rules()
        {
            return ChangedRules.Vigor
                || Customized.Any(Settings.VigorMultiplier, Settings.RegenCurveStrength,
                    Settings.RegenCurvePivot, Settings.RegenPerExtraStaminaPoint, Settings.CountFoodStaminaOnly,
                    Settings.SneakSkillRegenBonus, Settings.BlockingRegenFactor);
        }

        [HarmonyPostfix]
        public static void Postfix(SEMan __instance, ref float staminaMultiplier)
        {
            if (!ChangedRules.StaminaRegenRules || !(__instance.m_character is Player player))
                return;

            if (ChangedRules.Vigor)
                staminaMultiplier += Vigor(player) / 100f;
            staminaMultiplier += ExtraStaminaBonus(player) / 100f;
            staminaMultiplier += SneakBonus(player) / 100f;
            staminaMultiplier *= CurveFactor(player);
            if (player.IsBlocking())
                staminaMultiplier *= NativeFeastMaster.Value(Settings.BlockingRegenFactor) / Settings.GameBlockingRegenFactor;
        }

        /// <summary>Runs after every other postfix, so the kept multiplier is the one the game goes on with.</summary>
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        public static void Remember(SEMan __instance, ref float staminaMultiplier) => StaminaCapture.Seen(__instance, staminaMultiplier);

        /// <summary>Total Vigor of the active foods in percent. Vigor does not fade with the food.</summary>
        public static float Vigor(Player player)
        {
            float total = 0f;
            foreach (Player.Food food in player.m_foods)
                total += ItemValues.VigorOf(food.m_item);
            return total;
        }

        private static float ExtraStaminaBonus(Player player)
        {
            float perPoint = NativeFeastMaster.Value(Settings.RegenPerExtraStaminaPoint);
            if (perPoint == 0f)
                return 0f;
            return Mathf.Max(0f, ExtraStamina(player)) * perPoint;
        }

        /// <summary>
        /// Max stamina above the player's base (Base Stamina when changed, else the game's or another mod's) and the
        /// skill bonus, or the active foods' current stamina.
        /// </summary>
        private static float ExtraStamina(Player player)
        {
            if (!NativeFeastMaster.Value(Settings.CountFoodStaminaOnly))
                return player.GetMaxStamina() - player.m_baseStamina - BaseValuesPatch.SkillBonus(player);

            float total = 0f;
            foreach (Player.Food food in player.m_foods)
                total += food.m_stamina;
            return total;
        }

        /// <summary>Crouched and standing still: the game's IsSneaking is crouching while moving.</summary>
        private static float SneakBonus(Player player)
        {
            float bonus = NativeFeastMaster.Value(Settings.SneakSkillRegenBonus);
            if (bonus == 0f || !player.IsCrouching() || player.IsSneaking())
                return 0f;
            return bonus * player.GetSkillFactor(Skills.SkillType.Sneak);
        }

        public static float CurveFactor(Player player)
        {
            float max = player.GetMaxStamina();
            float fraction = max > 0f ? player.GetStamina() / max : 1f;
            return RegenCurve.Factor(fraction, NativeFeastMaster.Value(Settings.RegenCurveStrength), NativeFeastMaster.Value(Settings.RegenCurvePivot));
        }
    }

    /// <summary>
    /// The bar-level regeneration curve shared by stamina and eitr: piecewise linear in the bar fraction s,
    /// strength M at s = 0, 1 at the pivot P, 1/M at s = 1. Strength 1 (or below 0) is off.
    /// </summary>
    public static class RegenCurve
    {
        public static float Factor(float fraction, float strength, float pivot)
        {
            if (strength <= 0f || Mathf.Approximately(strength, 1f))
                return 1f;
            fraction = Mathf.Clamp01(fraction);
            pivot = Mathf.Clamp01(pivot);
            if (fraction < pivot)
                return Mathf.Lerp(strength, 1f, fraction / pivot);
            float above = 1f - pivot;
            return above <= 0f ? 1f : Mathf.Lerp(1f, 1f / strength, (fraction - pivot) / above);
        }
    }

    /// <summary>
    /// Regeneration while encumbered or swimming. The game zeroes regeneration in both states; this rule
    /// applies the game's own formula at the configured fraction, with the same multipliers, the regen delay
    /// timer and Game.m_staminaRegenRate, capped at max stamina. The game's other zero conditions (attacking,
    /// dodging, wall running) still stop regeneration. Asked by <see cref="UpdateStatsPatch"/> after UpdateStats.
    /// </summary>
    public static class RestrictedStaminaRegen
    {
        public static bool Rules() => Customized.Any(Settings.EncumberedRegenFraction, Settings.SwimmingRegenFraction, Settings.SwimmingRegenDelay);

        public static void Apply(Player player, float dt)
        {
            bool safeBlocked = SafeStamina.AllowsBlockedRegeneration(player);
            if (player.InIntro() || player.IsTeleporting() ||
                (!safeBlocked && (player.InAttack() || player.InDodge() || player.m_wallRunning)))
                return;
            // Select one contribution: safe blocked frames replace, never add to, Recovery swimming regeneration.
            float fraction = safeBlocked ? 1f : Fraction(player);
            if (fraction <= 0f)
                return;

            float maxStamina = player.GetMaxStamina();
            if (player.m_stamina >= maxStamina || player.m_staminaRegenTimer > 0f)
                return;

            float regen = Regen(player, maxStamina) * (player.IsBlocking() ? Settings.GameBlockingRegenFactor : 1f) * fraction;
            player.m_stamina = Mathf.Min(maxStamina, player.m_stamina + regen * dt * Game.m_staminaRegenRate);
            player.m_nview.GetZDO().Set(ZDOVars.s_stamina, player.m_stamina);
        }

        /// <summary>
        /// The fraction of normal regen allowed by the player's state; 0 when the game already regenerates.
        /// Encumbered wins over swimming when both apply.
        /// </summary>
        private static float Fraction(Player player)
        {
            if (player.IsEncumbered())
                return Mathf.Clamp01(NativeFeastMaster.Value(Settings.EncumberedRegenFraction));
            if (!player.IsSwimming() || player.IsOnGround())
                return 0f;
            return SwimmingRegenDelayPatch.DelayElapsed() ? Mathf.Clamp01(NativeFeastMaster.Value(Settings.SwimmingRegenFraction)) : 0f;
        }

        private static float Regen(Player player, float maxStamina)
        {
            float regen = player.m_staminaRegen + (1f - player.m_stamina / maxStamina) * player.m_staminaRegen * player.m_staminaRegenTimeMultiplier;
            if (!StaminaCapture.TryGet(player, out float multiplier))
            {
                multiplier = 1f;
                player.m_seman.ModifyStaminaRegen(ref multiplier);
            }
            return regen * multiplier;
        }
    }

    /// <summary>Remembers the last swimming stroke (the game drains stamina when the swim input exceeds 0.1).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSwimming))]
    public static class SwimmingRegenDelayPatch
    {
        public static bool Prepare() => ChangedRules.RestrictedRegen;

        private static float lastStrokeTime = -1000f;

        [HarmonyPostfix]
        public static void Postfix(Vector3 targetVel)
        {
            if (targetVel.magnitude > 0.1f)
                lastStrokeTime = Time.time;
        }

        public static bool DelayElapsed()
        {
            return Time.time - lastStrokeTime >= Mathf.Max(0f, NativeFeastMaster.Value(Settings.SwimmingRegenDelay));
        }
    }
}
