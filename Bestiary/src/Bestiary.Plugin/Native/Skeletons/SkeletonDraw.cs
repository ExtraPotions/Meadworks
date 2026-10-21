// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Arsenal;
using ExtraPotions.Bestiary.Runtime.Native.Crossbow;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Skeletons
{
    /// <summary>
    /// One draw for every spawn of the game's Black Forest skeleton, wherever and whenever it spawns (the wild spawns
    /// at night once Bonemass is dead, the fixed spawners of burial chambers and the like, bone piles): the plain
    /// skeleton, each arsenal skeleton (<see cref="ArsenalKind"/>) and the crossbowman (<see cref="XbowKind"/>) are
    /// equally likely (the user, 2026-09-30). A spawn of the no-archer skeleton draws among the plain one and the
    /// melee arsenal skeletons only: no bowman or crossbowman where the game wants no archer. One switched off in the
    /// settings drops out of the draw, so the rest stay equal. Decided by whoever runs the spawner, the only machine
    /// that spawns anything. The spawn keeps its levels; only which creature comes changes.
    /// </summary>
    public static class SkeletonDraw
    {
        /// <summary>The spawn data for another skeleton in place of this wild spawn, or null to let it be.</summary>
        public static SpawnSystem.SpawnData? Wild(SpawnSystem.SpawnData critter)
        {
            GameObject? instead = Instead(critter.m_prefab);
            if (instead == null)
            {
                return null;
            }
            SpawnSystem.SpawnData data = critter.Clone();
            data.m_prefab = instead;
            return data;
        }

        /// <summary>The bone pile's pick, or the skeleton drawn with the same levels in its place.</summary>
        public static SpawnArea.SpawnData BonePile(SpawnArea.SpawnData picked)
        {
            GameObject? instead = picked == null ? null : Instead(picked.m_prefab);
            if (instead == null)
            {
                return picked!;
            }
            return new SpawnArea.SpawnData
            {
                m_prefab = instead, m_weight = picked!.m_weight, m_minLevel = picked.m_minLevel, m_maxLevel = picked.m_maxLevel,
            };
        }

        /// <summary>The draw's candidates, one list reused by every spawn (spawns run on the main thread, one at a time).</summary>
        private static readonly List<GameObject> Others = new List<GameObject>();

        /// <summary>The skeleton drawn to spawn in place of this one, or null when the draw is this one itself.</summary>
        public static GameObject? Instead(GameObject? prefab)
        {
            List<GameObject> others = Others;
            others.Clear();
            AddArsenal(others, prefab);
            AddCrossbowman(others, prefab);
            if (others.Count == 0)
            {
                return null;
            }
            int drawn = Random.Range(0, others.Count + 1);   // one more than the others: the skeleton itself
            return drawn < others.Count ? others[drawn] : null;
        }

        /// <summary>Each arsenal skeleton switched on, the bowman only in place of a skeleton that could be an archer.</summary>
        private static void AddArsenal(List<GameObject> others, GameObject? prefab)
        {
            ArsenalKind? kind = ArsenalKind.OfSource(prefab, out bool archer);
            if (kind == null)
            {
                return;
            }
            foreach (ArsenalWeapon weapon in ArsenalWeapon.All)
            {
                if ((archer || !weapon.IsBow) && ArsenalSettings.Spawns(weapon) && kind.Creatures.TryGetValue(weapon, out GameObject creature))
                {
                    others.Add(creature);
                }
            }
        }

        /// <summary>The crossbowman (an arsenal skeleton, switched with them), in place of the archer skeleton only.</summary>
        private static void AddCrossbowman(List<GameObject> others, GameObject? prefab)
        {
            XbowKind? kind = XbowKind.OfSkeleton(prefab);
            if (kind?.Prefab != null && ArsenalSettings.CrossbowmanSpawns)
            {
                others.Add(kind.Prefab);
            }
        }
    }
}
