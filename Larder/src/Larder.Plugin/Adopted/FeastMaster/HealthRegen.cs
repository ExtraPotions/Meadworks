// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/HealthRegen.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Continuous food healing: the game heals the sum of the active foods' Health Regen once every 10 seconds
    /// from m_foodRegenTimer. With the setting on, the same amount is spread over the 10 seconds, healed a little every frame
    /// (no floating numbers) and the timer is held at 0 so the game's tick never fires. Turning the setting off
    /// lets the game's timer resume. Only the owner runs UpdateFood with a dt, so no owner check is needed.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
    public static class ContinuousFoodHealingPatch
    {
        public static bool Prepare() => Customized.Any(Settings.ContinuousFoodHealing);

        private const float TickSeconds = 10f;

        [HarmonyPrefix]
        public static void Prefix(Player __instance, float dt, bool forceUpdate)
        {
            if (forceUpdate || dt <= 0f || !NativeFeastMaster.Value(Settings.ContinuousFoodHealing))
                return;

            __instance.m_foodRegenTimer = 0f;
            float perTick = RegenPerTick(__instance);
            if (perTick > 0f)
                __instance.Heal(perTick * dt / TickSeconds, showText: false);
        }

        private static float RegenPerTick(Player player)
        {
            float total = 0f;
            foreach (Player.Food food in player.m_foods)
                total += food.m_item.m_shared.m_foodRegen;
            if (total <= 0f)
                return 0f;

            float multiplier = 1f;
            player.m_seman.ModifyHealthRegen(ref multiplier);
            return total * multiplier;
        }
    }
}
