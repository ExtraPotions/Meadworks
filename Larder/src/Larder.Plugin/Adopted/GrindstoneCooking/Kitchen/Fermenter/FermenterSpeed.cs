// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterSpeed.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Fermenting speed from the Cooking level of whoever put the base in. The barrel stores only a start time;
    /// GetFermentationTime returns the world seconds since then (-1 when empty), and GetStatus compares that with
    /// m_fermentationDuration. Every readiness decision (status, visuals, hover, interact, the owner's tap check,
    /// DropAllItems) goes through GetStatus, so scaling the elapsed time here speeds up all of them on every peer
    /// alike, from the level in the barrel's ZDO. m_fermentationDuration is left alone: FeastMaster replaces it for
    /// each GetStatus call, and the two compose.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetFermentationTime))]
    internal static class FermenterSpeed
    {
        [HarmonyPostfix]
        private static void Postfix(Fermenter __instance, ref double __result)
        {
            if (!NativeCooking.Active || __result <= 0.0)
                return;
            __result *= Multiplier(__instance);
        }

        internal static float Multiplier(Fermenter barrel) => NativeCooking.Active
            ? Perks.FermentingSpeed(FermenterBase.GetLevel(barrel.m_nview.GetZDO())) : 1f;
    }
}
