// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using ExtraPotions.Wildblood.Adopted.Patches;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider's coins drop as it dies (<see cref="RaidCoins"/>), from the start of the game's <c>Character.OnDeath</c>:
    /// every death, whatever killed it, passes there on the creature's owner, and only there is its ZDO still whole (the
    /// game resets it at the end). A plain destroy (a raider walking off and vanishing, an area unloading) never passes
    /// here, so it pays nothing. A failure is reported and swallowed: nothing here may stop a creature dying.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class RaidCoinPatch
    {
        // Every death on every machine comes through here: a creature that is no raider costs two field reads and one
        // ZDO lookup; only a raider goes on to the owner test.
        private static void Prefix(Character __instance)
        {
            ZNetView nview = __instance.m_nview;
            ZDO? zdo = nview != null ? nview.GetZDO() : null;
            if (RaiderTag.IsRaider(zdo) && nview!.IsOwner())
            {
                SafeCall.Run("Character.OnDeath raid coins", static (raider, data) => RaidCoins.Drop(raider, data),
                    __instance, zdo!);
            }
        }
    }
}
