// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original approved floor-rain behavior using own-game API members only; no Carturs_Flooring source copied.
using HarmonyLib;

namespace Timber.Flooring
{
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
    public static class FloorRainWear
    {
        [HarmonyPrefix]
        public static void Prefix(WearNTear __instance, float time)
        {
            if (!FiresRuntime.RainProtectionActive || __instance.m_piece == null || !FloorRoofTags.IsFloor(__instance.m_piece)) return;
            var view = __instance.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            // Advance only the native rain damage countdown. The original update still computes wet visuals,
            // burning eligibility, ash/fire/attack damage and ordinary support; no whole UpdateWear suppression.
            __instance.m_rainTimer = time;
        }
    }
}
