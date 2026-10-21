// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// Where giants come from: at most one per mountain, and only on the `Mountains` share of them that hold one
    /// (<see cref="RimeMountains"/>). The giant's own entry in every zone's spawn system (the game's, so it spawns the
    /// way wild creatures do, decided by whoever owns the zone) rolls `Chance` percent once every `Interval` seconds in
    /// each zone of a listed biome someone is in; the spawn goes ahead only on a mountain whose giant has not come yet
    /// (<see cref="Allow"/>), which it then claims for good. A giant comes alone, away from the biome's edges, 40 to 80
    /// metres from a player like any wild spawn, never while another is loaded within <see cref="Apart"/> metres, and
    /// asleep (<see cref="RimeSlumber"/>). The entry follows the settings on every change; switched off, it spawns nothing
    /// and the giants already in the world stay.
    /// </summary>
    public static class RimeGiantSpawns
    {
        private const float Apart = 1000f;

        private static SpawnSystemList? list;
        private static readonly SpawnSystem.SpawnData Entry = new SpawnSystem.SpawnData
        {
            m_name = RimeGiantPrefabs.Creature,
            m_biomeArea = Heightmap.BiomeArea.Median,
            m_maxSpawned = 1,
            m_spawnDistance = Apart,
            m_groupSizeMin = 1,
            m_groupSizeMax = 1,
            m_huntPlayer = false,
            m_groundOffset = 0.05f,   // it sleeps with its body fixed in place, so it is set on the ground, not dropped
            m_minLevel = 1,
            m_maxLevel = 1,
        };

        /// <summary>A zone's spawn system as it wakes: our list joins its own (once the giant exists to spawn).</summary>
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

        /// <summary>At the moment of spawning, on the zone's owner: only on a mountain whose giant is still waiting.</summary>
        public static bool Allow(Vector3 point)
        {
            int region = RimeMountains.Waiting(point);
            if (region < 0)
            {
                return false;
            }
            RimeMountains.Claim(region);
            return true;
        }

        /// <summary>The entry after a build or a settings change.</summary>
        public static void Refresh()
        {
            Entry.m_prefab = RimeGiantPrefabs.Prefab;
            Entry.m_enabled = RimeGiantSettings.On;
            Entry.m_spawnChance = RimeGiantSettings.Chance;
            Entry.m_spawnInterval = RimeGiantSettings.Interval;
            Entry.m_biome = RimeGiantSettings.Biomes;
        }

        private static SpawnSystemList Make()
        {
            var holder = new GameObject("ECP_RimeGiant_spawns");
            holder.transform.SetParent(PrefabBench.Root, false);
            var made = holder.AddComponent<SpawnSystemList>();
            made.m_spawners.Add(Entry);
            return made;
        }
    }
}
