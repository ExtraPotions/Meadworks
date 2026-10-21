#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CameraArea.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
using System;
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// Where the camera may be: around every loaded crafting station (<c>CraftingStation.m_allStations</c>, placement
    /// ghosts excluded by the game), its build range with extensions (<c>GetStationBuildRange</c>) times Range
    /// Multiplier, measured flat as the game measures build range, and as far above or below the station. Read only: no
    /// station's range, marker or base area is changed, so raids and comfort keep the station's real area.
    /// </summary>
    public static class CameraArea
    {
        /// <summary>A clamped point lands just inside the edge, so the next frame counts it as inside.</summary>
        private const float Inset = 0.99f;

        public static bool Contains(Vector3 point)
        {
            foreach (CraftingStation station in CraftingStation.m_allStations)
            {
                if (station != null && Outside(station, point) <= 0f)
                    return true;
            }
            return false;
        }

        /// <summary>The point itself when inside an area, else the nearest point of the nearest area.</summary>
        public static Vector3 Clamp(Vector3 point)
        {
            return ClampToStations(point);
        }

        private static Vector3 ClampToStations(Vector3 point)
        {
            CraftingStation nearest = null;
            float best = float.MaxValue;
            foreach (CraftingStation station in CraftingStation.m_allStations)
            {
                if (station == null)
                    continue;
                float outside = Outside(station, point);
                if (outside <= 0f)
                    return point;
                if (outside < best)
                {
                    best = outside;
                    nearest = station;
                }
            }
            return nearest != null ? Into(nearest, point) : point;
        }

        private static float Range(CraftingStation station) => station.GetStationBuildRange() * CameraSettings.RangeMultiplier.Value;

        /// <summary>How far the point lies outside the station's area: 0 or less inside; a station without build range has none.</summary>
        private static float Outside(CraftingStation station, Vector3 point)
        {
            float range = Range(station);
            if (range <= 0f)
                return float.MaxValue;
            Vector3 centre = station.transform.position;
            float flat = Utils.DistanceXZ(centre, point) - range;
            float height = Mathf.Abs(point.y - centre.y) - range;
            if (flat <= 0f && height <= 0f)
                return Mathf.Max(flat, height);
            return Mathf.Max(flat, 0f) + Mathf.Max(height, 0f);
        }

        private static Vector3 Into(CraftingStation station, Vector3 point)
        {
            float range = Range(station) * Inset;
            Vector3 centre = station.transform.position;
            Vector2 flat = Vector2.ClampMagnitude(new Vector2(point.x - centre.x, point.z - centre.z), range);
            float y = Mathf.Clamp(point.y, centre.y - range, centre.y + range);
            return new Vector3(centre.x + flat.x, y, centre.z + flat.y);
        }
    }
}
