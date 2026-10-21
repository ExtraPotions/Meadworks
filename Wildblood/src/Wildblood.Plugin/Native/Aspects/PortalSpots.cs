// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Where a Portalbound far portal may open, decided on the boss's owner. A spot is drawn at random around the target:
    /// in a random direction, a random distance out on the ground plane, and at least `min height` above whatever is
    /// under it there - the ground, a building, a treetop or the sea's surface - plus a little more at random. It is
    /// kept only if it is within `range` of the target, at least `clearance` from any solid thing or creature (the boss's
    /// own body included), and has a clear line to the target's middle, so the throw it sends can reach. A bounded
    /// number of draws is tried; when none fits there is no portal and the attack is thrown as the game made it.
    /// </summary>
    internal static class PortalSpots
    {
        private const int Tries = 24;

        /// <summary>Metres a portal may hang above `min height`, at random, so portals are not all at one height.</summary>
        private const float HeightSpread = 3f;

        /// <summary>The nearest a portal opens to its target on the ground plane, so it is never straight overhead.</summary>
        private const float NearEdge = 4f;

        /// <summary>How far above the highest point in play the floor is looked for from.</summary>
        private const float SkyMargin = 50f;

        private static readonly int FloorMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "terrain", "vehicle");

        private static readonly int ClearMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "piece_nonsolid", "terrain", "vehicle", "character", "character_net", "character_noenv");

        /// <summary>A spot for the far portal around <paramref name="target"/> (the target's middle), or false.</summary>
        public static bool TryFind(Vector3 target, PortalSettings settings, out Vector3 spot)
        {
            for (int i = 0; i < Tries; i++)
            {
                if (Draw(target, settings, out spot) && Fits(spot, target, settings))
                {
                    return true;
                }
            }
            spot = Vector3.zero;
            return false;
        }

        // A random point around the target, lifted `min height` and a little more above what is under it.
        private static bool Draw(Vector3 target, PortalSettings settings, out Vector3 spot)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float reach = Random.Range(Mathf.Min(NearEdge, settings.Range * 0.5f), settings.Range);
            spot = target + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * reach;
            if (!Floor(spot, target.y + settings.Range + settings.MinHeight + SkyMargin, out float floor))
            {
                return false;
            }
            spot.y = floor + settings.MinHeight + Random.Range(0f, HeightSpread);
            return true;
        }

        // The top of whatever stands at this point, looked for from high above; never below the sea's surface.
        private static bool Floor(Vector3 point, float fromY, out float floor)
        {
            Vector3 start = new Vector3(point.x, fromY, point.z);
            bool hit = Physics.Raycast(start, Vector3.down, out RaycastHit found, fromY + 1000f, FloorMask,
                QueryTriggerInteraction.Ignore);
            ZoneSystem zones = ZoneSystem.instance;
            floor = hit ? found.point.y : 0f;
            if (hit && zones != null)
            {
                floor = Mathf.Max(floor, zones.m_waterLevel);
            }
            return hit;
        }

        private static bool Fits(Vector3 spot, Vector3 target, PortalSettings settings) =>
            Vector3.Distance(spot, target) <= settings.Range
            && !Physics.CheckSphere(spot, Mathf.Max(0.01f, settings.Clearance), ClearMask, QueryTriggerInteraction.Ignore)
            && !Physics.Linecast(spot, target, FloorMask, QueryTriggerInteraction.Ignore);
    }
}
