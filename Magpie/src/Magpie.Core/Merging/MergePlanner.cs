// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Magpie.Items;

namespace ExtraPotions.Magpie.Merging
{
    /// <summary>
    /// Chooses which ground stacks merge in one scan (design B1 to B8, 3.3 step 1). Pure and deterministic: the same items
    /// in any order give the same plans. Behavior follows SwiftPickup's documented merge (radius, minimum count, merges per
    /// scan, partial fill); the owner, settle, magnet, intent and identity rules are Meadworks rules.
    /// </summary>
    public static class MergePlanner
    {
        private static readonly ItemIdentityKey Keys = new ItemIdentityKey();

        public static IReadOnlyList<MergePlan> Plan(IReadOnlyList<GroundItem> items, MergeSettings settings, double worldTime, Func<string> nonce)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (nonce == null) throw new ArgumentNullException(nameof(nonce));

            // Target priority: larger stack first; ties go to the older ZDO, then the lower handle.
            var candidates = items
                .Where(item => item != null && Eligible(item, settings))
                .Select(item => (Item: item, Key: Keys.Key(item.Identity)))
                .Where(entry => entry.Key != null)
                .OrderByDescending(entry => entry.Item.Stack)
                .ThenBy(entry => entry.Item.Created)
                .ThenBy(entry => entry.Item.Id)
                .ToList();

            Dictionary<string, SpatialGroup>? groups = null;
            bool indexNeeded = false;
            var radiusSquared = settings.Radius * settings.Radius;
            var used = new HashSet<long>();
            var plans = new List<MergePlan>();
            foreach (var target in candidates)
            {
                if (plans.Count >= settings.MaxMergesPerScan)
                    break;
                if (used.Contains(target.Item.Id) || target.Item.Stack >= target.Item.MaxStack)
                    continue;
                if (indexNeeded && groups == null)
                {
                    groups = new Dictionary<string, SpatialGroup>(StringComparer.Ordinal);
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        var candidate = candidates[index];
                        if (!groups.TryGetValue(candidate.Key!, out var group))
                            groups.Add(candidate.Key!, group = new SpatialGroup(settings.Radius));
                        group.Add(candidate.Item, index);
                    }
                }
                GroundItem? source;
                int nearby;
                if (groups != null)
                    source = groups[target.Key!].Source(target.Item, radiusSquared, used, settings.MinCount, out nearby);
                else
                {
                    var near = candidates.Where(other => other.Item.Id != target.Item.Id
                        && string.Equals(other.Key, target.Key, StringComparison.Ordinal)
                        && other.Item.Position.DistanceSquared(target.Item.Position) <= radiusSquared).Select(other => other.Item).ToList();
                    nearby = near.Count;
                    source = nearby + 1 < settings.MinCount ? null : near.Where(other => !used.Contains(other.Id) && other.Stack < other.MaxStack)
                        .OrderBy(other => other.Stack).ThenByDescending(other => other.Created).ThenByDescending(other => other.Id).FirstOrDefault();
                }
                if (nearby + 1 < settings.MinCount || source == null)
                {
                    // Small lists and scans that immediately merge keep the original path.
                    indexNeeded = candidates.Count > 64;
                    continue;
                }
                var moved = Math.Min(source.Stack, target.Item.MaxStack - target.Item.Stack);
                if (moved <= 0)
                    continue;
                used.Add(target.Item.Id);
                used.Add(source.Id);
                plans.Add(new MergePlan(target.Item, source, moved, nonce(), worldTime));
            }
            return plans;
        }

        sealed class SpatialGroup
        {
            readonly double cellSize;
            readonly List<(GroundItem Item, int Order)> all = new List<(GroundItem, int)>();
            List<(GroundItem Item, int Order)>? unindexed;
            Dictionary<(int X, int Y, int Z), List<(GroundItem Item, int Order)>>? cells;

            // Twice the radius also covers float subtraction rounding at a cell boundary.
            internal SpatialGroup(float radius) { cellSize = 2d * radius; }

            internal void Add(GroundItem item, int order)
            {
                all.Add((item, order));
            }

            internal GroundItem? Source(GroundItem target, float radiusSquared, HashSet<long> used, int minimum, out int nearby)
            {
                nearby = 0;
                if (all.Count < minimum) return null;
                GroundItem? source = null;
                int sourceOrder = int.MaxValue;
                if (!Cell(target.Position, out var cell))
                {
                    foreach (var entry in all) Consider(entry, target, radiusSquared, used, ref nearby, ref source, ref sourceOrder);
                    return source;
                }
                if (cells == null)
                {
                    cells = new Dictionary<(int, int, int), List<(GroundItem, int)>>();
                    unindexed = new List<(GroundItem, int)>();
                    foreach (var entry in all)
                    {
                        if (!Cell(entry.Item.Position, out var location)) { unindexed.Add(entry); continue; }
                        if (!cells.TryGetValue(location, out var bucket)) cells.Add(location, bucket = new List<(GroundItem, int)>());
                        bucket.Add(entry);
                    }
                }
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++)
                            if (cells.TryGetValue((cell.X + x, cell.Y + y, cell.Z + z), out var bucket))
                                foreach (var entry in bucket) Consider(entry, target, radiusSquared, used, ref nearby, ref source, ref sourceOrder);
                foreach (var entry in unindexed!) Consider(entry, target, radiusSquared, used, ref nearby, ref source, ref sourceOrder);
                return source;
            }

            static void Consider((GroundItem Item, int Order) entry, GroundItem target, float radiusSquared, HashSet<long> used,
                ref int nearby, ref GroundItem? source, ref int sourceOrder)
            {
                var item = entry.Item;
                if (item.Id == target.Id || !(item.Position.DistanceSquared(target.Position) <= radiusSquared)) return;
                nearby++;
                if (used.Contains(item.Id) || item.Stack >= item.MaxStack) return;
                // Order retains the original stable tie when all source priority fields match.
                if (source == null || item.Stack < source.Stack || item.Stack == source.Stack &&
                    (item.Created > source.Created || item.Created == source.Created &&
                    (item.Id > source.Id || item.Id == source.Id && entry.Order < sourceOrder)))
                { source = item; sourceOrder = entry.Order; }
            }

            bool Cell(Point3 position, out (int X, int Y, int Z) cell)
            {
                double x = Math.Floor(position.X / cellSize), y = Math.Floor(position.Y / cellSize), z = Math.Floor(position.Z / cellSize);
                cell = default;
                // Leave room for neighboring cells; unusual coordinates keep the original distance test.
                if (!(x > int.MinValue && x < int.MaxValue && y > int.MinValue && y < int.MaxValue && z > int.MinValue && z < int.MaxValue))
                    return false;
                cell = ((int)x, (int)y, (int)z);
                return true;
            }
        }

        /// <summary>Stackable, owned, valid, settled, not pulled, no open intent, not ignored (B6, B7, 4.4).</summary>
        public static bool Eligible(GroundItem item, MergeSettings settings) =>
            item.Valid
            && item.OwnedLocally
            && item.MaxStack > 1
            && item.Stack > 0
            && !item.Moving
            && !item.Pulled
            && !item.PendingIntent
            && item.AgeSeconds >= settings.SettleSeconds
            && item.Identity != null
            && !settings.Ignores(item.Identity.Prefab);
    }
}
