// Wildblood. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT, from SaveYourLuckBruh 1.0.2.
// Pin ece802da9f806f3ee9d6e50552beec479abae4ce. MIT notice: THIRD_PARTY_NOTICES/SaveYourLuckBruh-MIT.txt.
// Changed by expDARE, 2026: Wildblood lifecycle, existing policy/codec and owned player/world persistence.
// Adapted from Patches/PlayerDestroyPatch.cs: source hook point/timing retained; inactive until explicitly installed.
using HarmonyLib;

namespace ExtraPotions.Wildblood.AdoptedLuck;

[HarmonyPatch(typeof(Player), nameof(Player.OnDestroy))]
internal static class PlayerDestroyPatch
{
    private static void Postfix(Player __instance)
    {
        if (__instance == Player.m_localPlayer || StorageManager.IsLoadedFor(__instance))
        {
            StorageManager.Clear();
        }
    }
}
