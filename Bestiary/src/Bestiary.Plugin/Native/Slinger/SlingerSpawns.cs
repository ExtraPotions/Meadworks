// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Slinger
{
    /// <summary>
    /// Where slingers come from: a Greydwarf about to spawn in one of the settings' `Biomes` is a slinger instead, at the
    /// settings' `Share` - in the wild (the game's spawn system) and from greydwarf nests (unless `Nests` is off). Decided by
    /// whoever runs the spawner, which is the only machine that spawns anything. The pack stays the same size and keeps
    /// its levels; only which creature comes changes. Camps and other fixed spawners keep their greydwarfs.
    /// </summary>
    public static class SlingerSpawns
    {
        /// <summary>The spawn data for a slinger in place of this wild spawn, or null to let it be.</summary>
        public static SpawnSystem.SpawnData? Wild(SpawnSystem.SpawnData critter, Vector3 point)
        {
            if (!Swap(critter.m_prefab, point, nest: false))
            {
                return null;
            }
            SpawnSystem.SpawnData slinger = critter.Clone();
            slinger.m_prefab = SlingerPrefabs.Prefab;
            return slinger;
        }

        /// <summary>The nest's pick, or a slinger with the same levels in its place.</summary>
        public static SpawnArea.SpawnData Nest(SpawnArea.SpawnData picked, Vector3 nest)
        {
            if (picked == null || !Swap(picked.m_prefab, nest, nest: true))
            {
                return picked!;
            }
            return new SpawnArea.SpawnData
            {
                m_prefab = SlingerPrefabs.Prefab, m_weight = picked.m_weight, m_minLevel = picked.m_minLevel, m_maxLevel = picked.m_maxLevel,
            };
        }

        private static bool Swap(GameObject? prefab, Vector3 point, bool nest)
        {
            if (!SlingerSettings.On || (nest && !SlingerSettings.Nests) || SlingerPrefabs.Prefab == null || prefab == null
                || prefab.name != SlingerPrefabs.Greydwarf || SlingerSettings.Share <= 0f)
            {
                return false;
            }
            return SlingerSettings.InBiome(WorldGenerator.instance.GetBiome(point)) && Random.value < SlingerSettings.Share;
        }
    }
}
