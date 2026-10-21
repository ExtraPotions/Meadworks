// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterDrop.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The game's Fermenter.DropAllItems drops the base while fermenting, or the meads when Ready, then clears the
    /// content. Nothing in the game calls it today (a destroyed barrel's OnDestroyed does nothing, so the batch is
    /// lost), but a mod or a later game version may. The barrel's <see cref="FermenterBase"/> level is cleared with the
    /// content.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.DropAllItems))]
    internal static class FermenterDrop
    {
        [HarmonyPostfix]
        private static void Postfix(Fermenter __instance)
        {
            if (__instance.GetContent() == 0)
                FermenterBase.Clear(__instance.m_nview.GetZDO());
        }
    }
}
