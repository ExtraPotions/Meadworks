// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterHover.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
// Changed by expDARE, 2026: base stars removed (GrindstoneSkills 0.15.0 removed item stars and this file upstream); the
// pinned content/ward visibility guard now fronts only the original Fermenter Timer line.
using System;
using HarmonyLib;
using ExtraPotions.Larder.Cooking;
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The barrel's hover text can show the remaining brew time (the local Fermenter Timer preference). The game's
    /// GetHoverText names the content (the base's shared name, localized) while fermenting and while a batch waits to be
    /// tapped. Without ward access the game leaves the content out, and so does this.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
    internal static class FermenterHover
    {
        [HarmonyPostfix]
        private static void Postfix(Fermenter __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || __instance.m_nview == null || !__instance.m_nview.IsValid() ||
                __instance.GetContent() == 0 || Localization.instance == null ||
                !PrivateArea.CheckAccess(__instance.transform.position, 0f, false))
                return;
            string name = Localization.instance.Localize(__instance.GetContentName());
            int at = string.IsNullOrEmpty(name) ? -1 : __result.IndexOf(name, StringComparison.Ordinal);
            // Respect native or another plugin's concealed content instead of appending private batch details.
            if (at < 0) return;
            // This local presentation adds no state/RPC and leaves empty, ready and cover warnings unchanged.
            if (DisplaySettings.FermenterTimer == null || !DisplaySettings.FermenterTimer.Value ||
                !__instance.m_hasRoof || __instance.m_exposed || __instance.GetStatus() != Fermenter.Status.Fermenting)
                return;
            string? remaining = FermentCountdown.Format(__instance.GetFermentationTime(),
                FermentationTimePatch.EffectiveDuration(__instance), FermenterSpeed.Multiplier(__instance));
            if (remaining != null)
                __result += "\n" + LarderText.Format("fermenter_remaining", remaining);
        }
    }
}
