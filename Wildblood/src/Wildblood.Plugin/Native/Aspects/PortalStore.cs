// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Portalbound boss's current pair of portals, in its ZDO: where the far portal hangs and the point it faces, when
    /// it opened, when the throw was let go through it (which is when the portal at the hand opens), and when both
    /// close - every moment on the shared clock. The owner writes them and the game replicates them to every client
    /// holding the boss, so the portals need no message of their own: each client opens and closes the same two portals
    /// at the same moments, a client that meets the boss mid-attack draws the rest of it, and portals whose owner left
    /// mid-attack still close at the time already written. Only the codec lives here.
    /// </summary>
    internal static class PortalStore
    {
        private static readonly int PortalAtHash = TraitKeys.PortalAt.GetStableHashCode();
        private static readonly int PortalSpotHash = TraitKeys.PortalSpot.GetStableHashCode();

        /// <summary>The point the far portal faces: the target's centre at the wind-up, then again at the release.</summary>
        private const string AimKey = "ecr_portal_aim";

        /// <summary>When the throw was let go (shared-clock ms); 0 until then, for each attack.</summary>
        private const string FireKey = "ecr_portal_fire";

        /// <summary>When both portals close (shared-clock ms).</summary>
        private const string ShutKey = "ecr_portal_shut";

        /// <summary>When the latest far portal opened; 0 before the first.</summary>
        public static long OpenedAt(ZDO zdo) => zdo.GetLong(PortalAtHash);

        public static long FiredAt(ZDO zdo) => zdo.GetLong(FireKey);

        public static long ShutAt(ZDO zdo) => zdo.GetLong(ShutKey);

        public static Vector3 Spot(ZDO zdo) => zdo.GetVec3(PortalSpotHash, Vector3.zero);

        public static Vector3 Aim(ZDO zdo) => zdo.GetVec3(AimKey, Vector3.zero);

        /// <summary>Owner only, at the wind-up: the far portal and its latest closing time, then the time every
        /// machine watches for a change.</summary>
        public static void Open(ZDO zdo, long atMs, Vector3 spot, Vector3 aim, long shutMs)
        {
            zdo.Set(TraitKeys.PortalSpot, spot);
            zdo.Set(AimKey, aim);
            zdo.Set(FireKey, 0L);
            zdo.Set(ShutKey, shutMs);
            zdo.Set(TraitKeys.PortalAt, atMs);
        }

        /// <summary>Owner only, as the throw is let go: where it is aimed, and when the portals close at the latest.</summary>
        public static void Fire(ZDO zdo, long atMs, Vector3 aim, long shutMs)
        {
            zdo.Set(AimKey, aim);
            zdo.Set(ShutKey, shutMs);
            zdo.Set(FireKey, atMs);
        }

        /// <summary>Owner only: the attack is over (or was cut short) and both portals close at this moment.</summary>
        public static void Shut(ZDO zdo, long shutMs) => zdo.Set(ShutKey, shutMs);
    }
}
