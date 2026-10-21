// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Core/RpcSendSwap.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The one patch on ZNetView.InvokeRPC(string, object[]), through which every object's RPCs leave every machine.
    /// A mead base into a fermenter (<see cref="FermenterAddSend"/>) tests its open add first, so any other send
    /// passes untouched. A swapped send skips the game's. Milling belongs to another product.
    /// </summary>
    [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.InvokeRPC), typeof(string), typeof(object[]))]
    public static class RpcSendSwap
    {
        [HarmonyPrefix]
        private static bool Prefix(ZNetView __instance, string method, object[] parameters) =>
            !FermenterAddSend.Swapped(__instance, method, parameters);
    }
}
