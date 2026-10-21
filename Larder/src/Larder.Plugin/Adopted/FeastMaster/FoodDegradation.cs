// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/FoodDegradation.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Food degradation: the game lowers each food's health/stamina/eitr over time in UpdateFood as
    /// full value x (remaining fraction ^ 0.3) and then sums them in GetTotalFoodValue. Recomputing the values with
    /// the configured exponent (or restoring the full values) right before the sum changes the curve without
    /// rewriting the game's IL. The HUD food bar reads the same Food objects, so it follows too.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
    public static class PlayerFoodDegradationPatch
    {
        public static bool Prepare() => Customized.Any(FeastMasterData.DisableFoodDegradation, FeastMasterData.DegradationCurve);

        private const float GameCurve = 0.3f;

        [HarmonyPrefix]
        public static void Prefix(Player __instance)
        {
            bool full = NativeFeastMaster.Value(FeastMasterData.DisableFoodDegradation);
            float curve = Mathf.Max(0f, NativeFeastMaster.Value(FeastMasterData.DegradationCurve));
            if (!full && Mathf.Approximately(curve, GameCurve))
                return;

            foreach (Player.Food food in __instance.m_foods)
                Recompute(food, full ? 1f : Strength(food, curve));
        }

        /// <summary>The food's current strength, 0..1, for the given exponent; 0 means no fading.</summary>
        public static float Strength(Player.Food food, float curve)
        {
            if (curve <= 0f)
                return 1f;
            float burnTime = food.m_item.m_shared.m_foodBurnTime;
            float remaining = burnTime > 0f ? Mathf.Clamp01(food.m_time / burnTime) : 0f;
            return Mathf.Pow(remaining, curve);
        }

        private static void Recompute(Player.Food food, float strength)
        {
            ItemDrop.ItemData.SharedData shared = food.m_item.m_shared;
            food.m_health = shared.m_food * strength;
            food.m_stamina = shared.m_foodStamina * strength;
            food.m_eitr = shared.m_foodEitr * strength;
        }
    }
}
