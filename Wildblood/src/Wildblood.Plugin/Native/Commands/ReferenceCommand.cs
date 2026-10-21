// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; Export seam for the owned console route.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// `wildblood reference`: gathers every creature the running game has registered - vanilla and modded alike, from the
    /// game's own prefab registry - with its biomes, health, boss flag and vanilla drop table, and hands the list to
    /// <see cref="ReferenceReport"/> to write. Generated rather than shipped so it can never be stale, never misses
    /// another mod's creatures, and is read from the game's own data alone (`loot.md` section 7).
    /// </summary>
    internal static class ReferenceCommand
    {
        public sealed class Entry
        {
            public string Prefab = "";
            public string Display = "";
            public float Health;
            public bool Boss;
            public Heightmap.Biome Biomes;
            public readonly List<CharacterDrop.Drop> Drops = new List<CharacterDrop.Drop>();
        }

        public static void Run(Terminal.ConsoleEventArgs args)
        {
            if (ZNetScene.instance == null)
            {
                CommandReply.Send(args, "wildblood reference: needs a loaded world.");
                return;
            }
            List<Entry> entries = Collect(SpawnBiomes());
            string? path = ReferenceReport.Write(entries);
            CommandReply.Send(args, path != null
                ? $"wildblood reference: wrote {entries.Count} creatures to {path}"
                : "wildblood reference: could not write the file - see the log.");
        }

        /// <summary>The read-only `wildblood reference` route: same collection and file, the caller words the reply.</summary>
        internal static string? Export(out int count)
        {
            List<Entry> entries = Collect(SpawnBiomes());
            count = entries.Count;
            return ReferenceReport.Write(entries);
        }

        private static List<Entry> Collect(Dictionary<string, Heightmap.Biome> biomes)
        {
            List<Entry> entries = new List<Entry>();
            foreach (GameObject prefab in Registry())
            {
                Character character = prefab != null ? prefab.GetComponent<Character>() : null!;
                if (character != null && !(character is Player))
                {
                    entries.Add(Describe(prefab!, character, biomes));
                }
            }
            return entries;
        }

        private static Entry Describe(GameObject prefab, Character character, Dictionary<string, Heightmap.Biome> biomes)
        {
            Entry entry = new Entry
            {
                Prefab = prefab.name,
                Display = character.m_name.TrimStart('$'),
                Health = character.m_health,
                Boss = character.m_boss,
                Biomes = biomes.TryGetValue(prefab.name, out Heightmap.Biome biome) ? biome : Heightmap.Biome.None,
            };
            CharacterDrop drop = prefab.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                entry.Drops.AddRange(drop.m_drops);
            }
            return entry;
        }

        // The game's full prefab registry - the reason modded creatures appear here with no list to maintain.
        private static IEnumerable<GameObject> Registry()
        {
            Dictionary<int, GameObject>? named = Traverse.Create(ZNetScene.instance)
                .Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>();
            return named != null ? (IEnumerable<GameObject>)named.Values : new List<GameObject>();
        }

        /// <summary>
        /// Which biomes spawn each prefab, from the live open-world spawn lists. Spawner-placed creatures (camps,
        /// dungeons) are not in these lists and land in the report's unassigned section instead: reading their
        /// locations would force-load every location asset, which is not worth a tidier grouping.
        /// </summary>
        private static Dictionary<string, Heightmap.Biome> SpawnBiomes()
        {
            Dictionary<string, Heightmap.Biome> map = new Dictionary<string, Heightmap.Biome>();
            foreach (SpawnSystemList list in SpawnLists())
            {
                foreach (SpawnSystem.SpawnData data in list.m_spawners)
                {
                    if (data.m_prefab != null)
                    {
                        map.TryGetValue(data.m_prefab.name, out Heightmap.Biome biome);
                        map[data.m_prefab.name] = biome | data.m_biome;
                    }
                }
            }
            return map;
        }

        // Every zone's SpawnSystem carries the same lists, so the first instance is as good as any.
        private static List<SpawnSystemList> SpawnLists()
        {
            List<SpawnSystem>? systems = Traverse.Create(typeof(SpawnSystem))
                .Field("m_instances").GetValue<List<SpawnSystem>>();
            return systems != null && systems.Count > 0 ? systems[0].m_spawnLists : new List<SpawnSystemList>();
        }
    }
}
