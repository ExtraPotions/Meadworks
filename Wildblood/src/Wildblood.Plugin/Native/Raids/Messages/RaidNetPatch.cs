// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Patches;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// World start, on every machine: the raids' routed messages are registered before any raid can send one (the
    /// owner sends, every client shows, so every machine needs the handler).
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    internal static class RaidNetPatch
    {
        private static void Postfix() => SafeCall.Run("ZoneSystem.Start raid messages", RaidNet.EnsureRegistered);
    }
}
