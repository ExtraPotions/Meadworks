// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Sea Ward (<c>ship_damage_taken</c>): the ship the player steers takes X% less damage. Every hit on a ship -
    /// creatures, the sea's impacts, Ashlands water, fire - reaches its <c>WearNTear.RPC_Damage</c>, which applies it on
    /// the ship's owner only (that is often not the helmsman). There the hit is scaled by 1 - X before the game's
    /// resistances, X being the published total of the player at the helm (<see cref="PlayerStats"/>, clamped to the
    /// running rules), read from that player's own ZDO. A ship nobody steers takes full damage.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_Damage))]
    internal static class ShipWardPatch
    {
        private static void Prefix(WearNTear __instance, HitData hit)
        {
            if (hit == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
            {
                return;
            }
            Ship? ship = __instance.GetComponent<Ship>();
            float ward = ship != null ? Mathf.Min(1f, PlayerStats.Of(Helmsman(ship), PlayerStats.ShipWard)) : 0f;
            if (ward > 0f)
            {
                hit.m_damage.Modify(1f - ward);
            }
        }

        private static Player? Helmsman(Ship ship)
        {
            ShipControlls? controls = ship.m_shipControlls;
            return controls != null && controls.HaveValidUser() ? Player.GetPlayer(controls.GetUser()) : null;
        }
    }
}
