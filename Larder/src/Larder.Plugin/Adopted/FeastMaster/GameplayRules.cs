// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/GameplayRules.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries;
// safe swimming suppresses only native drowning damage, including the zero-stamina admission boundary.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>Eat Again At: the game allows re-eating below half of the food's duration.</summary>
    [HarmonyPatch(typeof(Player.Food), nameof(Player.Food.CanEatAgain))]
    public static class EatAgainPatch
    {
        public static bool Prepare() => Customized.Any(FeastMasterData.EatAgainAt) || NativeFeastMaster.EatAnything;

        private const float GameFraction = 0.5f;

        [HarmonyPostfix]
        public static void Postfix(Player.Food __instance, ref bool __result)
        {
            float fraction = NativeFeastMaster.EatAnything ? 1f : Mathf.Clamp01(NativeFeastMaster.Value(FeastMasterData.EatAgainAt));
            if (Mathf.Approximately(fraction, GameFraction))
                return;
            __result = __instance.m_time < __instance.m_item.m_shared.m_foodBurnTime * fraction;
        }
    }

    /// <summary>
    /// Drowning Damage: Player.OnSwimming builds a HitType.Drowning hit of Ceil(maxHealth / 20) each second without
    /// stamina and passes it to Character.Damage; the damage is scaled there for players.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class DrowningDamagePatch
    {
        public static bool Prepare() => Customized.Any(Settings.DrowningDamage) || SafeStamina.Applies(SafeStaminaAction.Swim);

        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || hit.m_hitType != HitData.HitType.Drowning || !(__instance is Player player))
                return;
            hit.m_damage.m_damage *= SafeStamina.ProtectsFromDrowning(player)
                ? 0f : Mathf.Max(0f, NativeFeastMaster.Value(Settings.DrowningDamage));
        }
    }
}
