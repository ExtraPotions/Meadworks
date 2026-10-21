// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Recap;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// This machine's own player dying starts a death recap (<see cref="RecapStore.OnDeath"/>): the hits so far are taken
    /// from the log and the video keeps recording a moment longer. Only the owner runs <c>Player.OnDeath</c> through; the
    /// prefix runs before it and must never stop a death, so a failure is reported and swallowed.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    public static class RecapDeathPatch
    {
        private static void Prefix(Player __instance)
        {
            if (ExtraPotions.Wildblood.Runtime.WildbloodRecap.Recording && __instance == Player.m_localPlayer && !__instance.IsDead())
            {
                SafeCall.Run("Player.OnDeath death recap", () => RecapStore.OnDeath(__instance));
            }
        }
    }
}
