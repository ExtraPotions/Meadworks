// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterTap.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Tapping, on the barrel's ZDO owner. The game's RPC_Tap (owner only, when Ready) clears the content and start
    /// time and spawns the meads a moment later. When RPC_Tap did tap (the content went from set to 0), the barrel's
    /// <see cref="FermenterBase"/> level is cleared with it, so the next batch starts from its own cook's level.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.RPC_Tap))]
    internal static class FermenterTap
    {
        [HarmonyPrefix]
        private static void Prefix(Fermenter __instance, out int __state) => __state = __instance.GetContent();

        [HarmonyPostfix]
        private static void Postfix(Fermenter __instance, int __state)
        {
            if (__state != 0 && __instance.GetContent() == 0)
                FermenterBase.Clear(__instance.m_nview.GetZDO());
        }
    }
}
