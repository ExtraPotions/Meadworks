// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/StaminaCostsMovement.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Movement stamina costs. Each site reads a field of the player for its drain; the field is scaled in a
    /// prefix for the duration of the call and restored in a finalizer, so equipment and status effect modifiers
    /// keep working and settings hot reload: nothing is written permanently.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CheckRun))]
    public static class RunCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.RunCost, Settings.OutOfCombatRunCost) || SafeStamina.Applies(SafeStaminaAction.Sprint);

        [HarmonyPrefix]
        public static void Prefix(Player __instance, out ScaledFields __state)
        {
            float multiplier = NativeFeastMaster.Value(Settings.RunCost) * CostRules.OutOfCombatFactor(__instance, NativeFeastMaster.Value(Settings.OutOfCombatRunCost));
            multiplier = SafeStamina.Multiplier(__instance, SafeStaminaAction.Sprint, multiplier);
            __state = CostRules.Scale(ref __instance.m_runStaminaDrain, multiplier);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Player __instance, ScaledFields __state) => CostRules.Restore(ref __instance.m_runStaminaDrain, __state);
    }

    /// <summary>
    /// Jump: Character.Jump checks HaveStamina(m_jumpStaminaUsage) and then calls Player.OnJump, which drains
    /// from the same field; scaling it around Jump covers both, so a free jump is never refused for lack of stamina.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
    public static class JumpCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.JumpCost, Settings.OutOfCombatJumpCost, Settings.SkillDiscount) || SafeStamina.Applies(SafeStaminaAction.Jump);

        [HarmonyPrefix]
        public static void Prefix(Character __instance, out ScaledFields __state)
        {
            __state = default;
            if (!(__instance is Player player))
                return;
            float multiplier = NativeFeastMaster.Value(Settings.JumpCost)
                * CostRules.OutOfCombatFactor(player, NativeFeastMaster.Value(Settings.OutOfCombatJumpCost))
                * CostRules.SkillDiscountFactor(player, Skills.SkillType.Jump);
            multiplier = SafeStamina.Multiplier(player, SafeStaminaAction.Jump, multiplier);
            __state = CostRules.Scale(ref __instance.m_jumpStaminaUsage, multiplier);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Character __instance, ScaledFields __state) => CostRules.Restore(ref __instance.m_jumpStaminaUsage, __state);
    }

    /// <summary>Dodge: the game's helper returns the cost after equipment, status effects and its own skill discount.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetDodgeStaminaUse))]
    public static class DodgeCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.DodgeCost, Settings.OutOfCombatDodgeCost, Settings.SkillDiscount);

        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref float __result)
        {
            __result *= NativeFeastMaster.Value(Settings.DodgeCost)
                * CostRules.OutOfCombatFactor(__instance, NativeFeastMaster.Value(Settings.OutOfCombatDodgeCost))
                * CostRules.SkillDiscountFactor(__instance, Skills.SkillType.Dodge);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSneaking))]
    public static class SneakCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.SneakCost, Settings.OutOfCombatSneakCost, Settings.FreeSneakingWithoutEnemies) || SafeStamina.Applies(SafeStaminaAction.Sneak);

        [HarmonyPrefix]
        public static void Prefix(Player __instance, out ScaledFields __state)
        {
            float multiplier = CostRules.SneakingIsFree(__instance)
                ? 0f
                : NativeFeastMaster.Value(Settings.SneakCost) * CostRules.OutOfCombatFactor(__instance, NativeFeastMaster.Value(Settings.OutOfCombatSneakCost));
            multiplier = SafeStamina.Multiplier(__instance, SafeStaminaAction.Sneak, multiplier);
            __state = CostRules.Scale(ref __instance.m_sneakStaminaDrain, multiplier);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Player __instance, ScaledFields __state) => CostRules.Restore(ref __instance.m_sneakStaminaDrain, __state);
    }

    /// <summary>Swim: the drain is a lerp between the min-skill and max-skill fields, both scaled.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSwimming))]
    public static class SwimCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.SwimCost) || SafeStamina.Applies(SafeStaminaAction.Swim);

        [HarmonyPrefix]
        public static void Prefix(Player __instance, out ScaledFields __state)
        {
            float multiplier = SafeStamina.Multiplier(__instance, SafeStaminaAction.Swim, NativeFeastMaster.Value(Settings.SwimCost));
            __state = CostRules.Scale(ref __instance.m_swimStaminaDrainMinSkill, ref __instance.m_swimStaminaDrainMaxSkill, multiplier);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Player __instance, ScaledFields __state)
        {
            CostRules.Restore(ref __instance.m_swimStaminaDrainMinSkill, ref __instance.m_swimStaminaDrainMaxSkill, __state);
        }
    }

    /// <summary>
    /// Encumbered: UpdateStats drains m_encumberedStaminaDrain per second while walking encumbered. Asked by
    /// <see cref="UpdateStatsPatch"/>, the one UpdateStats patch.
    /// </summary>
    public static class EncumberedCostRule
    {
        public static bool Rules() => Customized.Any(Settings.EncumberedCost) || SafeStamina.Applies(SafeStaminaAction.Encumbered);

        public static ScaledFields Scale(Player player) =>
            CostRules.Scale(ref player.m_encumberedStaminaDrain, SafeStamina.Multiplier(player, SafeStaminaAction.Encumbered,
                Mathf.Max(0f, NativeFeastMaster.Value(Settings.EncumberedCost))));

        public static void Restore(Player player, ScaledFields saved) => CostRules.Restore(ref player.m_encumberedStaminaDrain, saved);
    }
}
