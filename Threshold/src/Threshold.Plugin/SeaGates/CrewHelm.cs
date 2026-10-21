// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewHelm.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The helm across a jump, by the game's own steering path (verified in the decompiled assembly): steering
    /// is the player's doodad controller (<c>Player.m_doodadController</c>), which for a ship is its
    /// <c>ShipControlls</c>; <c>Player.GetControlledShip</c> returns the ship it steers. Letting go is
    /// <c>Player.StopDoodadControl</c>, which calls <c>ShipControlls.OnUseStop</c>: a <c>ReleaseControl</c> RPC to the
    /// ship's owner and <c>AttachStop</c> (the player stands at the helm's detach offset, 0.5 m up). Taking it is the
    /// <c>RequestControl</c> RPC on the ship's <c>ZNetView</c> with the player id, the one call
    /// <c>ShipControlls.Interact</c> makes: the owner grants it only while its own copy of the ship has the player
    /// aboard (<c>IsPlayerInBoat</c>, a trigger) and the helm is free or already theirs, and answers
    /// <c>RequestRespons</c>, which starts the doodad control and seats the player at the helm. After a landing the
    /// owner may not see the player aboard yet, so the request is repeated until it is answered.</summary>
    internal static class CrewHelm
    {
        private const float RetrySeconds = 0.5f;
        private const float GiveUpSeconds = 8f;

        private static ZDOID pending = ZDOID.None;
        private static float until;
        private static float nextTry;

        /// <summary>Lets go of the helm or any other controls and gets up from a seat or bed. True when the player
        /// held this ship's helm.</summary>
        internal static bool Release(Player player, Ship ship)
        {
            bool hadHelm = ship != null && player.GetControlledShip() == ship;
            if (player.GetDoodadController() != null)
                player.StopDoodadControl();
            if (player.m_attached)
                player.AttachStop();
            return hadHelm;
        }

        /// <summary>After landing: ask for the helm of this ship until the owner answers or a few seconds pass.</summary>
        internal static void Request(ZDOID ship)
        {
            pending = ship;
            until = Time.time + GiveUpSeconds;
            nextTry = 0f;
        }

        internal static void Clear() => pending = ZDOID.None;

        /// <summary>Every physics step of the local player outside a jump; works only while a request is pending.</summary>
        internal static void Tick(Player player)
        {
            if (pending.IsNone() || Time.time < nextTry)
                return;
            nextTry = Time.time + RetrySeconds;
            Ship ship = CrewShip.Find(pending);
            if (Time.time > until || Done(player, ship))
            {
                Clear();
                return;
            }
            if (ship == null || ship.m_nview == null || !ship.m_nview.IsValid() || !ship.IsPlayerInBoat(player))
                return;
            ship.m_nview.InvokeRPC("RequestControl", player.GetPlayerID());
        }

        /// <summary>Nothing left to ask: the player steers it, moved on (seated, steering something else, dead,
        /// encumbered, as <c>ShipControlls.Interact</c> refuses), or someone else took the helm meanwhile.</summary>
        private static bool Done(Player player, Ship ship)
        {
            if (player.IsDead() || player.IsTeleporting() || player.IsEncumbered())
                return true;
            if (ship == null)
                return false;
            if (player.GetControlledShip() == ship || player.m_attached || player.GetDoodadController() != null)
                return true;
            ShipControlls controls = ship.m_shipControlls;
            return controls == null || (controls.HaveValidUser() && controls.GetUser() != player.GetPlayerID());
        }
    }
}
