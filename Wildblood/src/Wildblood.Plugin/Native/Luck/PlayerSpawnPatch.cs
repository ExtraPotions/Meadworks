// Wildblood. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT, from SaveYourLuckBruh 1.0.2.
// Pin ece802da9f806f3ee9d6e50552beec479abae4ce. MIT notice: THIRD_PARTY_NOTICES/SaveYourLuckBruh-MIT.txt.
// Changed by expDARE, 2026: Wildblood lifecycle, existing policy/codec and owned player/world persistence.
// Adapted from Patches/PlayerSpawnPatch.cs: source hook point/timing retained; inactive until explicitly installed.
using HarmonyLib;

namespace ExtraPotions.Wildblood.AdoptedLuck;

[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
internal static class PlayerSpawnPatch
{
    private static void Postfix(Player __instance)
    {
        if (!StorageManager.IsPersistenceEnabled)
        {
            return;
        }

        if (__instance == Player.m_localPlayer && ZNet.instance != null && ZNet.m_world != null)
        {
            long worldUID = ZNet.instance.GetWorldUID();
            StorageManager.EnsureLoaded(__instance, worldUID);
        }
    }
}
