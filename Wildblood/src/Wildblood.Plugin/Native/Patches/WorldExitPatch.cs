// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Leaving a world - a logout or the game quitting, on every machine: the game takes down every object of the world
    /// here, and what this mod counted for that world's creatures (the Splintering cascades) goes with it, so nothing
    /// carries into the next world joined in the same session.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown))]
    public static class WorldExitPatch
    {
        private static void Postfix() => DescendantRegistry.Clear();
    }
}
