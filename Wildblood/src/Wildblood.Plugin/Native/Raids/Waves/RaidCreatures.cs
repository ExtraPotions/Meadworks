// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The creatures a game event brings, read from its own spawn list (<c>RandomEvent.m_spawn</c>), so a raid brings
    /// what the game's raid of that name brings and another mod's event brings its own. Every peer holds the same list,
    /// so an entry is addressed by its place in it and a wave plan written on one machine reads the same on the next. An
    /// entry comes when the game would spawn it in this world: switched on, a creature prefab, and the global key it
    /// names (if any) set, the test the game's spawner makes before each spawn.
    /// </summary>
    internal static class RaidCreatures
    {
        /// <summary>The game's switched-on event of that name; null when this game has none (a mod since removed).</summary>
        public static RandomEvent? Event(string name)
        {
            RandEventSystem? system = RandEventSystem.instance;
            if (system == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            foreach (RandomEvent ev in system.m_events)
            {
                if (ev != null && ev.m_enabled && ev.m_name == name)
                {
                    return ev;
                }
            }
            return null;
        }

        /// <summary>Fills <paramref name="into"/> with the places in the event's spawn list of the entries that come.</summary>
        public static void Entries(RandomEvent ev, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < ev.m_spawn.Count; i++)
            {
                if (Comes(ev.m_spawn[i]))
                {
                    into.Add(i);
                }
            }
        }

        /// <summary>True when the event brings at least one creature in this world.</summary>
        public static bool AnyComes(RandomEvent ev)
        {
            for (int i = 0; i < ev.m_spawn.Count; i++)
            {
                if (Comes(ev.m_spawn[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The entry at a place in the event's list; null for a place the list does not have.</summary>
        public static SpawnSystem.SpawnData? At(RandomEvent ev, int place) =>
            place >= 0 && place < ev.m_spawn.Count ? ev.m_spawn[place] : null;

        /// <summary>The creature an entry spawns, read off its prefab.</summary>
        public static Character? Creature(SpawnSystem.SpawnData data) =>
            data.m_prefab != null ? data.m_prefab.GetComponent<Character>() : null;

        /// <summary>The game's limit for the entry in its raid: how many may be about at once. 0 in the game means no limit
        /// on the count, only on the pace; here it counts as one.</summary>
        public static int Limit(SpawnSystem.SpawnData data) => Mathf.Max(1, data.m_maxSpawned);

        /// <summary>The position in <paramref name="entries"/> of the creature with the most health: the Warlord's kind.
        /// The first in the list wins a tie.</summary>
        public static int Toughest(RandomEvent ev, List<int> entries)
        {
            int best = 0;
            float most = float.MinValue;
            for (int i = 0; i < entries.Count; i++)
            {
                Character? creature = Creature(ev.m_spawn[entries[i]]);
                float health = creature != null ? creature.m_health : 0f;
                if (health > most)
                {
                    best = i;
                    most = health;
                }
            }
            return best;
        }

        /// <summary>The position in <paramref name="entries"/> of the creature that comes most (the highest limit): the
        /// face the raid is named after. The first in the list wins a tie.</summary>
        public static int Commonest(RandomEvent ev, List<int> entries)
        {
            int best = 0;
            for (int i = 1; i < entries.Count; i++)
            {
                if (Limit(ev.m_spawn[entries[i]]) > Limit(ev.m_spawn[entries[best]]))
                {
                    best = i;
                }
            }
            return best;
        }

        private static bool Comes(SpawnSystem.SpawnData? data) =>
            data != null && data.m_enabled && data.m_prefab != null && Creature(data) != null && KeySet(data.m_requiredGlobalKey);

        private static bool KeySet(string key) =>
            string.IsNullOrEmpty(key) || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key));
    }
}
