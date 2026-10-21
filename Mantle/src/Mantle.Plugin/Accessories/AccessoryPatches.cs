// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Accessories
{
    /// <summary>
    /// The game's questions that the rings and the belt answer (<see cref="AccessoryBonus"/>), each for the local player only
    /// and each adding exactly what the game's own status effects add, so the bonuses stack with theirs. Maximum health,
    /// stamina and eitr come from the food totals the game sets them from (re-read every second, so a slot change shows within
    /// a second); the carry weight, the stamina regeneration, the speed and an attack's stamina use are the game's own
    /// modifier methods. All patches are owned by this plugin's Harmony instance and removed with it.
    /// </summary>
    public static class AccessoryPatches
    {
        private static bool Local(Character character) => character != null && character == Player.m_localPlayer;

        private static bool Local(SEMan seman) => seman != null && Player.m_localPlayer != null && ReferenceEquals(seman, Player.m_localPlayer.GetSEMan());

        [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
        public static class FoodTotals
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance, ref float hp, ref float stamina, ref float eitr)
            {
                if (!Local(__instance))
                    return;
                AccessoryStats bonus = AccessoryBonus.Current();
                hp += bonus.MaxHealth;
                stamina += bonus.MaxStamina;
                eitr += bonus.MaxEitr;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
        public static class Carry
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance, ref float __result)
            {
                if (!Local(__instance))
                    return;
                float carry = AccessoryBonus.Current().Carry;
                if (carry != 0f)
                    __result = Mathf.Max(0f, __result + carry * Game.m_carryWeightRate);
            }
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyStaminaRegen))]
        public static class StaminaRegen
        {
            [HarmonyPostfix]
            public static void Postfix(SEMan __instance, ref float staminaMultiplier)
            {
                if (Local(__instance))
                    staminaMultiplier += AccessoryBonus.Current().StaminaRegenAdd;
            }
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ApplyStatusEffectSpeedMods))]
        public static class Speed
        {
            [HarmonyPrefix]
            public static void Prefix(float speed, out float __state) => __state = speed;

            [HarmonyPostfix]
            public static void Postfix(SEMan __instance, ref float speed, float __state)
            {
                if (Local(__instance))
                    speed += AccessoryBonus.Current().SpeedAdd(__state);
            }
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAttackStaminaUsage))]
        public static class AttackStamina
        {
            [HarmonyPostfix]
            public static void Postfix(SEMan __instance, float baseStaminaUse, ref float staminaUse, bool minZero)
            {
                if (Local(__instance))
                    staminaUse = AccessoryBonus.Current().AttackStaminaApplied(baseStaminaUse, staminaUse, minZero);
            }
        }
    }
}
