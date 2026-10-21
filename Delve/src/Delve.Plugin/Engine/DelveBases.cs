// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the player base guard (design B6, General / Player Base Guard): wards and player base areas near a
// site, read on the authority from object data and prefab components, so it works on a dedicated server with nothing
// loaded.
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;
using UnityEngine;

namespace ExtraPotions.Delve.Runtime
{
    public static class DelveBases
    {
        /// <summary>Prefab hash to area radius: wards (PrivateArea, only while enabled) and pieces with a player base effect area.</summary>
        private static readonly Dictionary<int, (float Radius, bool Ward)> areas = new Dictionary<int, (float, bool)>();
        private static float largest;

        public static void Index()
        {
            areas.Clear();
            largest = 0f;
            if (ZNetScene.instance == null) return;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                float radius = 0f;
                bool ward = false;
                var privateArea = prefab.GetComponentInChildren<PrivateArea>(true);
                if (privateArea != null)
                {
                    radius = privateArea.m_radius;
                    ward = true;
                }
                foreach (var effect in prefab.GetComponentsInChildren<EffectArea>(true))
                {
                    if ((effect.m_type & EffectArea.Type.PlayerBase) == 0) continue;
                    var sphere = effect.GetComponent<SphereCollider>();
                    if (sphere != null) radius = Mathf.Max(radius, sphere.radius * effect.transform.lossyScale.x);
                }
                if (radius <= 0f) continue;
                areas[prefab.name.GetStableHashCode()] = (radius, ward);
                largest = Mathf.Max(largest, radius);
            }
        }

        public static void Clear()
        {
            areas.Clear();
            largest = 0f;
        }

        /// <summary>Base and ward areas whose objects stand near the spot (player-built only; a disabled ward does not count).</summary>
        public static List<Area> Near(Vector3 spot)
        {
            var found = new List<Area>();
            if (areas.Count == 0 || ZDOMan.instance == null) return found;
            foreach (ZDO zdo in DelveWorld.ObjectsNear(spot, largest + 8f))
            {
                if (!areas.TryGetValue(zdo.GetPrefab(), out var area)) continue;
                if (zdo.GetLong(ZDOVars.s_creator) == 0L) continue;
                if (area.Ward && !zdo.GetBool(ZDOVars.s_enabled)) continue;
                found.Add(new Area(DelveWorld.ToVec(zdo.GetPosition()), area.Radius));
            }
            return found;
        }
    }
}
