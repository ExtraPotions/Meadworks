// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Luck;
using UnityEngine;

namespace ExtraPotions.Wildblood.AdoptedLuck;

internal static class NativeWeightedPity
{
    internal static List<PitySource>? Catalog()
    {
        var scene = ZNetScene.instance;
        if (scene == null) return null;
        var result = new List<PitySource>();
        foreach (var prefab in scene.m_namedPrefabs.Values)
        {
            var drop = prefab != null ? prefab.GetComponent<CharacterDrop>() : null;
            if (drop == null) continue;
            if (drop.m_drops == null) return null;
            foreach (var row in drop.m_drops)
                if (row != null && row.m_prefab != null)
                    result.Add(new PitySource(row.m_prefab.name, row.m_chance, row.m_amountMin, row.m_amountMax, row.m_levelMultiplier));
        }
        return result;
    }

    internal static bool SafeSource(CharacterDrop drop, CharacterDrop.Drop row, IReadOnlyList<PitySource> catalog)
    {
        if (row.m_prefab == null || !WeightedPity.Eligible(row.m_chance, row.m_amountMin, row.m_amountMax) ||
            drop.m_drops.Count(other => other != null && other.m_prefab == row.m_prefab) != 1) return false;
        string name = row.m_prefab.name;
        if (!catalog.Any(source => source.Prefab == name && source.Chance == row.m_chance &&
            source.Minimum == row.m_amountMin && source.Maximum == row.m_amountMax && source.LevelMultiplier == row.m_levelMultiplier)) return false;
        for (int targetWeight = 1; targetWeight <= (row.m_levelMultiplier ? 1 << WeightedPity.MaximumStars : 1); targetWeight *= 2)
        {
            float target = row.m_chance * targetWeight;
            if (target > .3f) break;
            int key = NativeLuckCounters.Bucket(name.GetStableHashCode(), target);
            foreach (var source in catalog)
                for (int weight = 1; weight <= (source.LevelMultiplier ? 1 << WeightedPity.MaximumStars : 1); weight *= 2)
                {
                    float chance = source.Chance * weight;
                    if (!(chance > 0f && chance <= .3f)) break;
                    if (NativeLuckCounters.Bucket(source.Prefab.GetStableHashCode(), chance) == key &&
                        (source.Prefab != name || source.Chance != row.m_chance || !source.Eligible || chance != target)) return false;
                }
        }
        return true;
    }

    internal static bool KnownAliases(LuckTable table, IDictionary<int, int> native, string prefab, float chance, bool scaled)
    {
        for (int weight = 1; weight <= (scaled ? 1 << WeightedPity.MaximumStars : 1); weight *= 2)
        {
            float alias = chance * weight;
            if (alias > .3f) break;
            if (native.ContainsKey(NativeLuckCounters.Bucket(prefab.GetStableHashCode(), alias)) && !table.TryGet(prefab, alias, out _)) return false;
        }
        return true;
    }

    internal static bool Apply(LuckTable table, IReadOnlyList<PitySource> catalog, LuckDelta delta)
    {
        if (!delta.Weighted || !WeightedPity.TryMerge(table, catalog, delta.Prefab, delta.Chance, out int? prior)) return false;
        int remaining = delta.NewCounter >= 0 ? delta.NewCounter : Math.Max(0, (prior ?? 0) - delta.Consumed);
        return WeightedPity.TryStore(table, catalog, delta.Prefab, delta.Chance, remaining);
    }
}
