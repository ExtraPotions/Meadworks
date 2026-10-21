// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Where krakens come from: the kraken's own entry in every zone's spawn system (the game's wild spawning, decided by
    /// whoever owns the zone). A zone of the Ocean that a player is in rolls `Chance` percent once every `Interval`
    /// seconds, for a point 60 to 100 metres from them over water at least `Min Depth` deep; the spawn goes ahead only
    /// with a ship carrying a crew within <see cref="ShipNear"/> metres (<see cref="Allow"/>), and never while another
    /// kraken is loaded within <see cref="Apart"/> metres. It comes up a few metres under the surface, not on the sea
    /// floor where the game would put it, and hunts that ship. The entry follows the settings on every change; switched
    /// off, it spawns nothing.
    /// </summary>
    public static class KrakenSpawns
    {
        private const float Apart = 1000f;
        private const float ShipNear = 150f;
        private const float UnderSurface = 3f;

        private static SpawnSystemList? list;
        private static readonly SpawnSystem.SpawnData Entry = new SpawnSystem.SpawnData
        {
            m_name = KrakenPrefabs.Creature,
            m_biome = Heightmap.Biome.Ocean,
            m_biomeArea = Heightmap.BiomeArea.Everything,
            m_maxSpawned = 1,
            m_spawnDistance = Apart,
            m_spawnRadiusMin = 60f,
            m_spawnRadiusMax = 100f,
            m_groupSizeMin = 1,
            m_groupSizeMax = 1,
            m_huntPlayer = false,
            m_minAltitude = -1000f,
            m_groundOffset = 0f,
            m_minLevel = 1,
            m_maxLevel = 1,
        };

        /// <summary>A zone's spawn system as it wakes: our list joins its own (once the kraken exists to spawn).</summary>
        public static void Join(SpawnSystem system)
        {
            if (Entry.m_prefab == null)
            {
                return;
            }
            list = list != null ? list : Make();
            if (!system.m_spawnLists.Contains(list))
            {
                system.m_spawnLists.Add(list);
                Adapters.NativeRuntime.SpawnList(system, list);
            }
        }

        /// <summary>At the moment of spawning, on the zone's owner: only near a crewed ship, and moved up near the surface.</summary>
        public static bool Allow(ref Vector3 point)
        {
            if (KrakenShips.Nearest(point, ShipNear) == null)
            {
                return false;
            }
            point.y = KrakenShips.Water(point) - UnderSurface;
            return true;
        }

        /// <summary>The entry after a build or a settings change.</summary>
        public static void Refresh()
        {
            Entry.m_prefab = KrakenPrefabs.Prefab;
            Entry.m_enabled = KrakenSettings.On;
            Entry.m_spawnChance = KrakenSettings.Chance;
            Entry.m_spawnInterval = KrakenSettings.Interval;
            Entry.m_maxAltitude = -KrakenSettings.MinDepth;   // the game measures from the sea floor under the point
        }

        private static SpawnSystemList Make()
        {
            var holder = new GameObject("ECP_Kraken_spawns");
            holder.transform.SetParent(PrefabBench.Root, false);
            var made = holder.AddComponent<SpawnSystemList>();
            made.m_spawners.Add(Entry);
            return made;
        }
    }
}
