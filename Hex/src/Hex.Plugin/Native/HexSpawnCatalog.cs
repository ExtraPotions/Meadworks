// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSpawnCatalog
{
    internal sealed class Entry
    {
        internal readonly HexSpawnKind Kind;
        internal readonly GameObject Prefab;
        internal readonly string Name, Category;
        internal readonly Sprite? Icon;
        internal readonly int MaxQuality;
        internal readonly bool Tameable;
        internal Entry(HexSpawnKind kind, GameObject prefab, string name, string category, Sprite? icon, int maximum, bool tameable)
        { Kind = kind; Prefab = prefab; Name = name; Category = category; Icon = icon; MaxQuality = maximum; Tameable = tameable; }
        internal string Identity => Prefab.name;
        internal bool Matches(string search) => HexSpawnData.Fold(Name + " " + Identity).Contains(HexSpawnData.Fold(search));
    }
    static string Local(string token) => Localization.instance?.Localize(token) ?? token;
    internal static IReadOnlyList<Entry> Items()
    {
        if (ObjectDB.instance == null || ZNetScene.instance == null) return Array.Empty<Entry>();
        var rows = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var listed in ObjectDB.instance.m_items)
        {
            if (listed == null || !HexSpawnData.PrefabValid(listed.name)) continue;
            var prefab = ZNetScene.instance.GetPrefab(listed.name); var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop?.m_itemData?.m_shared == null || prefab.GetComponent<ZNetView>() == null) continue;
            var item = drop.m_itemData;
            if (item.m_shared.m_maxQuality < 1 || item.m_shared.m_maxStackSize < 1) continue;
            rows[prefab.name] = new Entry(HexSpawnKind.Item, prefab, HexSpawnData.DisplayName(Local(item.m_shared.m_name), prefab.name), item.m_shared.m_itemType.ToString(),
                item.m_shared.m_icons != null && item.m_shared.m_icons.Length > 0 ? item.m_shared.m_icons[0] : null, item.m_shared.m_maxQuality, false);
        }
        return rows.Values.OrderBy(row => row.Name, StringComparer.CurrentCulture).ThenBy(row => row.Identity, StringComparer.Ordinal).ToArray();
    }
    internal static IReadOnlyList<Entry> Creatures()
    {
        if (ZNetScene.instance == null) return Array.Empty<Entry>();
        var biomes = new Dictionary<string, Heightmap.Biome>(StringComparer.Ordinal);
        foreach (var system in SpawnSystem.m_instances)
            if (system != null) foreach (var list in system.m_spawnLists)
                if (list != null) foreach (var spawn in list.m_spawners)
                    if (spawn?.m_prefab != null) biomes[spawn.m_prefab.name] = (biomes.TryGetValue(spawn.m_prefab.name, out var previous) ? previous : Heightmap.Biome.None) | spawn.m_biome;
        var rows = new List<Entry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prefab in ZNetScene.instance.m_prefabs)
        {
            if (prefab == null || !HexSpawnData.PrefabValid(prefab.name) || !seen.Add(prefab.name) || prefab.GetComponent<Player>() != null || prefab.GetComponent<ZNetView>() == null) continue;
            var character = prefab.GetComponent<Character>();
            if (character == null) continue;
            var category = biomes.TryGetValue(prefab.name, out var biome) && biome != Heightmap.Biome.None ? biome.ToString() : "Unclassified";
            // Native Tameable.Tame requires MonsterAI; no custom taming implementation is guessed.
            var tameable = prefab.GetComponent<Tameable>() != null && prefab.GetComponent<MonsterAI>() != null;
            rows.Add(new Entry(HexSpawnKind.Creature, prefab, HexSpawnData.DisplayName(Local(character.m_name), prefab.name), category, null, HexSpawnData.MaxCreatureLevel, tameable));
        }
        return rows.OrderBy(row => row.Name, StringComparer.CurrentCulture).ThenBy(row => row.Identity, StringComparer.Ordinal).ToArray();
    }
    internal static Entry? Resolve(HexSpawnRequest request)
        => (request.Kind == HexSpawnKind.Item ? Items() : Creatures()).FirstOrDefault(row => row.Identity.Equals(request.Prefab, StringComparison.Ordinal));
}
