// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Display;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Keeps the world tier box under the minimap current (<see cref="TierHud"/>). The HUD updates every frame whether
    /// or not the inventory is open, which is what a box always in view needs; the box touches its text only when the
    /// tier changes. Never on a dedicated server, which has no HUD.
    /// </summary>
    [HarmonyPatch(typeof(Hud), "Update")]
    public static class HudTierPatch
    {
        private static void Postfix() => Guard.Run("Hud world tier", TierHud.Refresh);
    }
}
