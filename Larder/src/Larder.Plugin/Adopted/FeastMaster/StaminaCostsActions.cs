// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/StaminaCostsActions.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>Block: normal and perfect block drains are fields of the Humanoid, scaled around BlockAttack.</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    public static class BlockCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.BlockCost, Settings.SkillDiscount);

        [HarmonyPrefix]
        public static void Prefix(Humanoid __instance, out ScaledFields __state)
        {
            __state = default;
            if (!(__instance is Player player))
                return;
            float multiplier = NativeFeastMaster.Value(Settings.BlockCost) * CostRules.SkillDiscountFactor(player, Skills.SkillType.Blocking);
            __state = CostRules.Scale(ref __instance.m_blockStaminaDrain, ref __instance.m_perfectBlockStaminaDrain, multiplier);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Humanoid __instance, ScaledFields __state)
        {
            CostRules.Restore(ref __instance.m_blockStaminaDrain, ref __instance.m_perfectBlockStaminaDrain, __state);
        }
    }

    /// <summary>
    /// Attack: the game's helper returns the cost after equipment, status effects and the weapon skill. Attacks of
    /// home items (hammer, hoe, cultivator swings) take the Tool Cost instead, like their placement.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackStamina))]
    public static class AttackCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.AttackCost, Settings.ToolCost) || SafeStamina.Applies(SafeStaminaAction.Attacks);

        [HarmonyPostfix]
        public static void Postfix(Attack __instance, ref float __result)
        {
            if (!(__instance.m_character is Player player))
                return;
            __result *= __instance.m_isHomeItem ? NativeFeastMaster.Value(Settings.ToolCost) : NativeFeastMaster.Value(Settings.AttackCost);
            __result = SafeStamina.Cost(player, SafeStaminaAction.Attacks, __result);
        }
    }

    /// <summary>Tool: placement with the hammer, hoe and cultivator.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetBuildStamina))]
    public static class ToolCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.ToolCost) || SafeStamina.Applies(SafeStaminaAction.Build);

        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref float __result)
        {
            __result *= NativeFeastMaster.Value(Settings.ToolCost);
            __result = SafeStamina.Cost(__instance, SafeStaminaAction.Build, __result);
        }
    }

    /// <summary>
    /// Fishing: FishingFloat.FixedUpdate drains the hooked cost per second while a fish is on the line and the
    /// pull cost per second while reeling in. The pull cost also includes the fish's own stamina use, scaled in
    /// <see cref="FishStaminaUsePatch"/>.
    /// </summary>
    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.FixedUpdate))]
    public static class FishingCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FishingHookedCost, Settings.FishingPullCost);

        [HarmonyPrefix]
        public static void Prefix(FishingFloat __instance, out ScaledGroup __state)
        {
            __state = new ScaledGroup
            {
                First = CostRules.Scale(ref __instance.m_hookedStaminaPerSec, ref __instance.m_hookedStaminaPerSecMaxSkill, NativeFeastMaster.Value(Settings.FishingHookedCost)),
                Second = CostRules.Scale(ref __instance.m_pullStaminaUse, NativeFeastMaster.Value(Settings.FishingPullCost)),
            };
        }

        [HarmonyFinalizer]
        public static void Finalizer(FishingFloat __instance, ScaledGroup __state)
        {
            CostRules.Restore(ref __instance.m_hookedStaminaPerSec, ref __instance.m_hookedStaminaPerSecMaxSkill, __state.First);
            CostRules.Restore(ref __instance.m_pullStaminaUse, __state.Second);
        }
    }

    /// <summary>The fish's contribution to the pull cost (only used by FishingFloat's pull drain).</summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.GetStaminaUse))]
    public static class FishStaminaUsePatch
    {
        public static bool Prepare() => Customized.Any(Settings.FishingPullCost);

        [HarmonyPostfix]
        public static void Postfix(ref float __result) => __result *= NativeFeastMaster.Value(Settings.FishingPullCost);
    }

    /// <summary>Harpoon: the status effect on the harpooned creature drains the puller's stamina from m_staminaDrain.</summary>
    [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
    public static class HarpoonCostPatch
    {
        public static bool Prepare() => Customized.Any(Settings.HarpoonCost);

        [HarmonyPrefix]
        public static void Prefix(SE_Harpooned __instance, out ScaledFields __state)
        {
            __state = CostRules.Scale(ref __instance.m_staminaDrain, NativeFeastMaster.Value(Settings.HarpoonCost));
        }

        [HarmonyFinalizer]
        public static void Finalizer(SE_Harpooned __instance, ScaledFields __state) => CostRules.Restore(ref __instance.m_staminaDrain, __state);
    }
}
