// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// World start, on every machine: the shriek's deafen message is registered before any Screecher can send it, so a
    /// player is reached even when their machine has not yet set up the creature that shrieked at them.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    public static class ShriekRpcPatch
    {
        private static void Postfix() => SafeCall.Run("ZoneSystem.Start screecher deafen", ShriekRpc.EnsureRegistered);
    }
}
