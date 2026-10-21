// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CargoCheck.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>Restricted cargo, by the portals' own rule (<c>Humanoid.IsTeleportable</c> and
    /// <c>Inventory.IsTeleportable</c> with <c>allowAllItems</c> false; the <c>TeleportAll</c> world key and the
    /// tool-tier exception apply as they do there). The ship's owner can read the ship's containers but not another
    /// player's pockets, so each client keeps its own player's <see cref="SeaGateFields.HeavyKey"/> current while
    /// aboard a ship, and the owner reads every other crew member's flag.</summary>
    public static class CargoCheck
    {
        private const float IntervalSeconds = 1f;

        private static readonly List<Container> containers = new List<Container>();
        private static float nextAt;

        /// <summary>Every frame on every machine with a local player (called by <see cref="SeaGates"/>); works
        /// once a second. Writes only when the flag changes, and clears it once after leaving the ship.</summary>
        public static void Tick()
        {
            if (Time.time < nextAt)
                return;
            nextAt = Time.time + IntervalSeconds;
            Player player = Player.m_localPlayer;
            ZDO zdo = OwnZdo(player);
            if (zdo == null)
                return;
            bool heavy = IsAboard(player) && !player.IsTeleportable(false);
            if (zdo.GetBool(SeaGateFields.HeavyKey) != heavy)
                zdo.Set(SeaGateFields.HeavyKey, heavy);
        }

        /// <summary>Ship owner: true when the ship may not jump because of what it or its crew carries.</summary>
        public static bool Blocked(Ship ship, out string reasonToken)
        {
            reasonToken = null;
            if (ship == null || SeaGateConfig.AllowRestrictedCargo.Value)
                return false;
            if (ContainersTeleportable(ship) && CrewTeleportable(ship))
                return false;
            reasonToken = SeaGateWords.DeniedCargo;
            return true;
        }

        /// <summary>The local player's own ZDO, only where this machine owns it.</summary>
        private static ZDO OwnZdo(Player player)
        {
            if (player == null || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner())
                return null;
            return player.m_nview.GetZDO();
        }

        /// <summary><c>Ship.s_currentShips</c> lists the ships whose trigger holds the local player; an entry can
        /// outlive its ship when the zone unloads (the ship has no OnDestroy), hence the null check.</summary>
        private static bool IsAboard(Player player)
        {
            foreach (Ship ship in Ship.s_currentShips)
            {
                if (ship != null && ship.IsPlayerInBoat(player))
                    return true;
            }
            return false;
        }

        /// <summary>Every container on the ship: the game's ships keep their chest's inventory in the ship's own ZDO
        /// (<c>Container.m_rootObjectOverride</c>), loaded into the live inventory at most a second late.</summary>
        private static bool ContainersTeleportable(Ship ship)
        {
            ship.GetComponentsInChildren(containers);
            try
            {
                foreach (Container container in containers)
                {
                    Inventory inventory = container != null ? container.GetInventory() : null;
                    if (inventory != null && !inventory.IsTeleportable(false))
                        return false;
                }
                return true;
            }
            finally
            {
                containers.Clear();
            }
        }

        /// <summary>The local player is checked directly, the others by the flag their own clients keep.</summary>
        private static bool CrewTeleportable(Ship ship)
        {
            foreach (Player player in ship.m_players)
            {
                if (player == null)
                    continue;
                bool heavy = player == Player.m_localPlayer ? !player.IsTeleportable(false) : HasHeavyFlag(player);
                if (heavy)
                    return false;
            }
            return true;
        }

        private static bool HasHeavyFlag(Player player)
        {
            ZNetView view = player.m_nview;
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && zdo.GetBool(SeaGateFields.HeavyKey);
        }
    }
}
