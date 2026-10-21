// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewLogout.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>A player who logs out mid-jump, or loses the server, is held over the water where the ship will be. The
    /// game saves the logout point from the player's position (<c>PlayerProfile.SaveLogoutPoint</c>, on logout, on a
    /// disconnect and on every save) and spawns them there next time (<c>Game.FindSpawnPoint</c>, which lifts a point
    /// below the ground onto it), so while a hold lasts the logout point is the dry spot beside the destination gate
    /// instead. A later save outside a jump writes the player's own position again. Not gated on the settings: it only
    /// acts during a hold.</summary>
    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SaveLogoutPoint))]
    public static class CrewLogoutPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerProfile __instance)
        {
            if (__instance == null || ZNet.instance == null || !CrewHold.TryLogoutPoint(out Vector3 point))
                return;
            __instance.SetLogoutPoint(point);
        }
    }
}
