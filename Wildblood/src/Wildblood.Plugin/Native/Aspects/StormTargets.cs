// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Who a Stormbound storm falls on and where the ground is. Every living player within `range` of the boss,
    /// measured on the ground plane - so a flying Moder, high above, still covers the players below it - gets a circle
    /// at their feet; a ghost or a debug flyer is never counted. A circle sits on whatever the player stands on: the
    /// terrain, a floor, a ship's deck, or the water's surface for a swimmer - never the sea bed under them.
    /// </summary>
    internal static class StormTargets
    {
        /// <summary>How far below a player's feet the floor is looked for; deeper is mid-air.</summary>
        private const float FeetSearch = 12f;

        private static readonly int FloorMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "terrain", "vehicle");

        /// <summary>The players the storm may strike this moment, into a reused list.</summary>
        public static List<Player> InRange(Vector3 boss, float range, List<Player> into)
        {
            into.Clear();
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player != null && !player.IsDead() && !player.InGhostMode() && !player.IsDebugFlying()
                    && !player.IsTeleporting() && FlatDistance(player.transform.position, boss) <= range
                    && SameSide(player.transform.position, boss))
                {
                    into.Add(player);
                }
            }
            return into;
        }

        // A dungeon's rooms sit far above its entrance at the same ground position: a player inside one is never in range
        // of a boss outside, nor the other way round.
        private static bool SameSide(Vector3 player, Vector3 boss) =>
            Character.InInterior(player) == Character.InInterior(boss);

        /// <summary>A circle's centre under a player: their feet dropped onto what they stand on.</summary>
        public static Vector3 Under(Vector3 feet) => new Vector3(feet.x, FloorY(feet, feet.y, 1f, FeetSearch), feet.z);

        /// <summary>
        /// The floor under a point, looked for from <paramref name="above"/> metres over <paramref name="fromY"/> down
        /// to <paramref name="depth"/> below it; <paramref name="fromY"/> itself when nothing is there. Never below the
        /// sea's surface, so a circle over water floats on it.
        /// </summary>
        public static float FloorY(Vector3 point, float fromY, float above, float depth)
        {
            Vector3 start = new Vector3(point.x, fromY + above, point.z);
            float y = Physics.Raycast(start, Vector3.down, out RaycastHit hit, above + depth, FloorMask,
                QueryTriggerInteraction.Ignore) ? hit.point.y : fromY;
            ZoneSystem zones = ZoneSystem.instance;
            return zones != null ? Mathf.Max(y, zones.m_waterLevel) : y;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>A direction flattened to the ground and normalised; zero when it has no horizontal part.</summary>
        public static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }
}
