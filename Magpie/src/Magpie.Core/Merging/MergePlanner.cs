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

            var radiusSquared = settings.Radius * settings.Radius;
            var used = new HashSet<long>();
            var plans = new List<MergePlan>();
            foreach (var target in candidates)
            {
                if (plans.Count >= settings.MaxMergesPerScan)
                    break;
                if (used.Contains(target.Item.Id) || target.Item.Stack >= target.Item.MaxStack)
                    continue;
                var near = candidates
                    .Where(other => other.Item.Id != target.Item.Id
                        && string.Equals(other.Key, target.Key, StringComparison.Ordinal)
                        && other.Item.Position.DistanceSquared(target.Item.Position) <= radiusSquared)
                    .Select(other => other.Item)
                    .ToList();
                if (near.Count + 1 < settings.MinCount)
                    continue;
                // Source: the smallest unused, non-full stack nearby; ties drain the newer ZDO first.
                var source = near
                    .Where(other => !used.Contains(other.Id) && other.Stack < other.MaxStack)
                    .OrderBy(other => other.Stack)
                    .ThenByDescending(other => other.Created)
                    .ThenByDescending(other => other.Id)
                    .FirstOrDefault();
                if (source == null)
                    continue;
                var moved = Math.Min(source.Stack, target.Item.MaxStack - target.Item.Stack);
                if (moved <= 0)
                    continue;
                used.Add(target.Item.Id);
                used.Add(source.Id);
                plans.Add(new MergePlan(target.Item, source, moved, nonce(), worldTime));
            }
            return plans;
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
