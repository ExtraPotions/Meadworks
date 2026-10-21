// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.9.0 at ValheimMods
// 3bb778330f23645ccddb42fbc0c6ffab72c842b6 (Core/StandIns.cs).
// Changed by expDARE, 2026: Unity-free, on prefab hashes passed in, so the counting rules are tested without the game.
using System.Collections.Generic;

namespace ExtraPotions.Bestiary.Spawning
{
    /// <summary>
    /// Bestiary's creatures that spawn in place of a game creature (the arsenal skeletons and crossbowmen for the
    /// Skeleton, the slinger for the Greydwarf), by prefab hash. The game's spawners cap what they spawn by prefab, so a
    /// stand-in must count as the creature it replaced: uncounted, a bone pile or greydwarf nest keeps spawning until the
    /// room is full, and a wild spawn passes its cap.
    /// </summary>
    public sealed class StandInTable
    {
        private readonly Dictionary<int, List<int>> replaces = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> standIns = new Dictionary<int, List<int>>();

        public void Add(int standIn, params int[] games)
        {
            foreach (int game in games)
            {
                AddOnce(replaces, standIn, game);
                AddOnce(standIns, game, standIn);
            }
        }

        /// <summary>Whether an untamed creature is a stand-in for one of a spawn area's own prefabs.</summary>
        public bool CountsFor(int creature, bool tamed, IEnumerable<int> areaPrefabs)
        {
            if (tamed || !replaces.TryGetValue(creature, out List<int> games))
                return false;
            foreach (int prefab in areaPrefabs)
                if (games.Contains(prefab))
                    return true;
            return false;
        }

        /// <summary>The stand-ins for a game creature among nearby objects (prefab, event flag), event creatures only when asked.</summary>
        public int Count(int game, IEnumerable<(int Prefab, bool Event)> objects, bool eventCreaturesOnly)
        {
            if (!standIns.TryGetValue(game, out List<int> theirs))
                return 0;
            int count = 0;
            foreach (var (prefab, isEvent) in objects)
                if (theirs.Contains(prefab) && (!eventCreaturesOnly || isEvent))
                    count++;
            return count;
        }

        public bool IsEmpty => replaces.Count == 0;

        public void Clear()
        {
            replaces.Clear();
            standIns.Clear();
        }

        private static void AddOnce(Dictionary<int, List<int>> map, int key, int value)
        {
            if (!map.TryGetValue(key, out List<int> values))
                map[key] = values = new List<int>();
            if (!values.Contains(value))
                values.Add(value);
        }
    }
}
