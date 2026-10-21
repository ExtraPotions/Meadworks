// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/EitrRegen.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Eitr regeneration rules through the game's hook for status effect multipliers: Eitr Vigor is added to the
    /// multiplier, then the eitr bar-level curve and the blocking factor scale it (the game's 0.8 while blocking
    /// becomes the configured factor). The game adds its equipment eitr regen modifier after this hook.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyEitrRegen))]
    public static class EitrRegenMultiplierPatch
    {
        public static bool Prepare()
        {
            return Customized.AnyFood(FeastMasterData.EitrVigor)
                || Customized.Any(Settings.EitrVigorPerEitrPoint, Settings.EitrVigorMultiplier, Settings.EitrRegenCurveStrength,
                    Settings.EitrRegenCurvePivot, Settings.BlockingEitrRegenFactor);
        }

        [HarmonyPostfix]
        public static void Postfix(SEMan __instance, ref float eitrMultiplier)
        {
            if (!(__instance.m_character is Player player))
                return;

            if (ChangedRules.EitrVigor)
                eitrMultiplier += EitrVigor(player) / 100f;
            eitrMultiplier *= CurveFactor(player);
            if (player.IsBlocking())
                eitrMultiplier *= NativeFeastMaster.Value(Settings.BlockingEitrRegenFactor) / Settings.GameBlockingRegenFactor;
        }

        /// <summary>Total Eitr Vigor of the active foods in percent. It does not fade with the food.</summary>
        public static float EitrVigor(Player player)
        {
            float total = 0f;
            foreach (Player.Food food in player.m_foods)
                total += ItemValues.EitrVigorOf(food.m_item);
            return total;
        }

        private static float CurveFactor(Player player)
        {
            float max = player.GetMaxEitr();
            float fraction = max > 0f ? player.GetEitr() / max : 1f;
            return RegenCurve.Factor(fraction, NativeFeastMaster.Value(Settings.EitrRegenCurveStrength), NativeFeastMaster.Value(Settings.EitrRegenCurvePivot));
        }
    }
}
