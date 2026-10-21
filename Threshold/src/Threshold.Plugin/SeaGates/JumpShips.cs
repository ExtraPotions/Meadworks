// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/JumpShips.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The ships loaded on this machine, for the owner's side of a jump (<see cref="ShipJump.Tick"/>). A ship
    /// joins on Awake; the game's <c>Ship</c> has no OnDestroy, so dead entries (a zone unloaded, a ship moved out of
    /// this machine's area mid-jump) are dropped whenever the list is read.</summary>
    public static class JumpShips
    {
        private static readonly List<Ship> ships = new List<Ship>();

        /// <summary>The live ships, dead entries dropped first.</summary>
        public static List<Ship> Live
        {
            get
            {
                ships.RemoveAll(ship => ship == null);
                return ships;
            }
        }

        internal static void Add(Ship ship)
        {
            if (!ships.Contains(ship))
                ships.Add(ship);
        }
    }

    /// <summary>Every ship with a ZDO registers <see cref="SeaGateFields.LandedRpc"/> (jump id, player id) and
    /// <see cref="SeaGateFields.PickRpc"/> (stop id, gate id) and joins
    /// <see cref="JumpShips"/> (and its prefab <see cref="ShipFreeze.IsShip"/>), whatever the settings say: a handler and a list entry change nothing in the game, and a
    /// ship loaded while sea gates are off must still be known when they are turned on. The handlers themselves check
    /// ownership and the jump or stop. A placement ghost has no ZDO (its <c>ZNetView</c> was disabled) and is
    /// skipped.</summary>
    [HarmonyPatch(typeof(Ship), "Awake")]
    public static class ShipJumpAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Ship __instance)
        {
            ZNetView view = __instance != null ? __instance.m_nview : null;
            if (view == null || view.GetZDO() == null)
                return;
            Ship ship = __instance;
            view.Register<int, long>(SeaGateFields.LandedRpc, (sender, jumpId, playerId) => ShipJump.OnLanded(ship, sender, jumpId, playerId));
            view.Register<int, long>(SeaGateFields.PickRpc, (sender, stopId, gateId) => JumpChoice.OnPick(ship, sender, stopId, gateId));
            JumpShips.Add(ship);
            ShipFreeze.KnowShip(view.GetZDO());
        }
    }
}
