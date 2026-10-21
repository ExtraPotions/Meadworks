// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    // Utility affixes on the local player's own bookkeeping. Each of these game methods runs on the player's own
    // client (skills, foods, comfort and the forsaken power are the player's, saved in its profile), and each patch
    // filters to Player.m_localPlayer.

    /// <summary>Soulbound: the death penalty's skill factor is X% smaller.</summary>
    [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
    internal static class SkillLossPatch
    {
        private static void Prefix(Skills __instance, ref float factor)
        {
            if (ReferenceEquals(__instance.m_player, Player.m_localPlayer))
            {
                factor *= Mathf.Max(0f, 1f - AggregateHost.Current[EffectKind.SkillLoss]);
            }
        }
    }

    /// <summary>
    /// Gourmand: the food just eaten gets X% of its burn time added. The game's food value is full while the remaining
    /// time is at or above the burn time, so the extra time is spent at full value before the normal fade.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
    internal static class FoodDurationPatch
    {
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            float longer = AggregateHost.Current[EffectKind.FoodDuration];
            if (!__result || longer <= 0f || !ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            foreach (Player.Food food in __instance.m_foods)
            {
                if (food.m_item.m_shared.m_name == item.m_shared.m_name)
                {
                    food.m_time += item.m_shared.m_foodBurnTime * longer;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Hearthbound: +X comfort. The game recomputes the comfort level every 2 s in Player.UpdateBaseValue (and resets
    /// its timer to 0 then); the bonus is added right after, so resting time and the HUD both use it. Patched here
    /// rather than on SE_Rested.CalculateComfortLevel, a tiny static the JIT may inline into its caller.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateBaseValue))]
    internal static class ComfortPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance.m_baseValueUpdateTimer == 0f && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                __instance.m_comfortLevel += Mathf.RoundToInt(AggregateHost.Current[EffectKind.RestComfort]);
            }
        }
    }

    /// <summary>Forsaken Favour: the cooldown the forsaken power just set is X% shorter.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
    internal static class ForsakenCooldownPatch
    {
        private static void Prefix(Player __instance, out float __state) => __state = __instance.m_guardianPowerCooldown;

        private static void Postfix(Player __instance, float __state)
        {
            if (__state <= 0f && __instance.m_guardianPowerCooldown > 0f && ReferenceEquals(__instance, Player.m_localPlayer))
            {
                __instance.m_guardianPowerCooldown *= Mathf.Max(0f, 1f - AggregateHost.Current[EffectKind.ForsakenCooldown]);
            }
        }
    }
}
