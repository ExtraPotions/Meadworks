// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using Biome = Heightmap.Biome;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Which biomes each creature prefab spawns in, from the game's open-world spawn lists (drops.md section 3, rule 3).
    /// Dungeon and camp spawners, summons and event creatures are not in these lists; they take the death-position
    /// biome instead. Built once, the first time it is asked after the zone system exists, on whichever peer rolls;
    /// the lists are static game data, identical on every peer with the same mods.
    /// </summary>
    internal static class SpawnBiomes
    {
        private static readonly Dictionary<string, Biome> ByPrefab = new Dictionary<string, Biome>(System.StringComparer.Ordinal);
        private static bool _built;

        /// <summary>False while the spawn lists cannot be found yet (main menu); callers must not cache a fallback then.</summary>
        public static bool Ready
        {
            get
            {
                EnsureBuilt();
                return _built;
            }
        }

        /// <summary>The union of spawn biomes for a prefab; false when no open-world list names it.</summary>
        public static bool TryGet(string prefab, out Biome biomes)
        {
            EnsureBuilt();
            return ByPrefab.TryGetValue(prefab, out biomes) && biomes != Biome.None;
        }

        private static void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }
            List<SpawnSystemList>? lists = Lists();
            if (lists == null)
            {
                return;
            }
            foreach (SpawnSystemList list in lists)
            {
                Add(list);
            }
            _built = true;
        }

        private static void Add(SpawnSystemList list)
        {
            if (list == null)
            {
                return;
            }
            foreach (SpawnSystem.SpawnData data in list.m_spawners)
            {
                if (data?.m_prefab == null || !data.m_enabled || data.m_devDisabled)
                {
                    continue;
                }
                string name = data.m_prefab.name;
                ByPrefab.TryGetValue(name, out Biome biome);
                ByPrefab[name] = biome | data.m_biome;
            }
        }

        // The zone control prefab carries the lists every zone's spawn system uses; a live instance is the fallback.
        private static List<SpawnSystemList>? Lists()
        {
            SpawnSystem? system = ZoneSystem.instance != null && ZoneSystem.instance.m_zoneCtrlPrefab != null
                ? ZoneSystem.instance.m_zoneCtrlPrefab.GetComponent<SpawnSystem>()
                : null;
            if (system != null && system.m_spawnLists != null && system.m_spawnLists.Count > 0)
            {
                return system.m_spawnLists;
            }
            List<SpawnSystem> live = SpawnSystem.m_instances;
            return live != null && live.Count > 0 && live[0] != null ? live[0].m_spawnLists : null;
        }
    }
}
