// Wildblood. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT, from SaveYourLuckBruh 1.0.2.
// Pin ece802da9f806f3ee9d6e50552beec479abae4ce. MIT notice: THIRD_PARTY_NOTICES/SaveYourLuckBruh-MIT.txt.
// Changed by expDARE, 2026: Wildblood lifecycle, existing policy/codec and owned player/world persistence.
// Adapted from Patches/PlayerSavePatch.cs: source hook point/timing retained; inactive until explicitly installed.
using HarmonyLib;
using System;

namespace ExtraPotions.Wildblood.AdoptedLuck;

[HarmonyPatch(typeof(Player), nameof(Player.Save))]
internal static class PlayerSavePatch
{
    private static void Prefix(Player __instance, out StorageManager.NativeSaveScope? __state)
    {
        __state = null;
        if (StorageManager.IsPersistenceEnabled && __instance == Player.m_localPlayer && ZNet.instance != null && ZNet.m_world != null)
        {
            long worldUID = ZNet.instance.GetWorldUID();
            StorageManager.SaveToCustomData(__instance, worldUID);
        }
        // Format 34 saves the process dictionary; keep personal/world counters out of native data.
        __state = StorageManager.SuspendForNativeSave();
    }

    private static Exception? Finalizer(Exception? __exception, StorageManager.NativeSaveScope? __state)
    { __state?.Restore(); return __exception; }
}
