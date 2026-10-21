// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood authority, lifecycle and existing stat/loot seams.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Horde
{
    /// <summary>
    /// Makes the world's spawn groups larger for one pass of the game's spawn list, then puts them back. The game's
    /// <c>SpawnData</c> entries are shared by every spawn system, so they are grown in place only for the length of one
    /// <c>SpawnSystem.UpdateSpawnList</c> call (<see cref="HordeSpawnPatch"/>) and restored by its finalizer, whatever
    /// happens in between: the group size range and the most of the kind allowed around the players, times the spawn
    /// multiplier, each rounded up or down at random so a fraction averages right. Each call owns its restoration so nested event calls cannot unwind another spawn pass.
    /// </summary>
    internal static class HordeGroups
    {
        private struct Saved
        {
            public int Max;
            public int GroupMin;
            public int GroupMax;
        }

        internal sealed class Pass
        {
            private List<SpawnSystem.SpawnData>? grown;
            private readonly List<Saved> originals = new List<Saved>();
            internal Pass(List<SpawnSystem.SpawnData> spawners) => grown = spawners;
            internal void Grow(float factor)
            {
                foreach (SpawnSystem.SpawnData data in grown!)
                {
                    originals.Add(new Saved { Max = data.m_maxSpawned, GroupMin = data.m_groupSizeMin, GroupMax = data.m_groupSizeMax });
                    GrowOne(data, factor);
                }
            }
            internal void Restore()
            {
                var list = grown; grown = null;
                if (list == null) return;
                for (int i = Mathf.Min(list.Count, originals.Count) - 1; i >= 0; i--)
                {
                    Saved saved = originals[i];
                    list[i].m_maxSpawned = saved.Max; list[i].m_groupSizeMin = saved.GroupMin; list[i].m_groupSizeMax = saved.GroupMax;
                }
            }
        }

        internal static Pass Grow(List<SpawnSystem.SpawnData> spawners, float factor)
        {
            var pass = new Pass(spawners);
            try { pass.Grow(factor); return pass; }
            catch { pass.Restore(); throw; }
        }

        // A most-allowed of 0 means no limit and stays so.
        private static void GrowOne(SpawnSystem.SpawnData data, float factor)
        {
            Character? creature = data.m_prefab != null ? data.m_prefab.GetComponent<Character>() : null;
            if (creature != null && creature.IsBoss()) return;
            if (data.m_maxSpawned > 0)
            {
                data.m_maxSpawned = Mathf.Max(1, Dice.Round(data.m_maxSpawned * factor));
            }
            data.m_groupSizeMin = Dice.Round(data.m_groupSizeMin * factor);
            data.m_groupSizeMax = Mathf.Max(data.m_groupSizeMin, Dice.Round(data.m_groupSizeMax * factor));
        }
    }
}
