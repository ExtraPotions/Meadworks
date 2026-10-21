// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Optional live Mantle Food cells over the existing Crucible capability registry; no inventory mutation or saved-grid inference.
using System;
using System.Collections.Generic;
using ExtraPotions.Larder.Eating;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Larder.Runtime
{
    public static class MantleFoodIntegration
    {
        public const string CapabilityId = "meadworks.mantle.foodslots";
        public const string ProviderId = "com.meadworks.mantle";
        static readonly System.Version MinimumVersion = new System.Version(1, 0);

        /// <summary>
        /// Choose one actual carried handle. Mantle publishes a fresh, read-only list of its local player's
        /// active Food cells as Func&lt;IReadOnlyList&lt;Tuple&lt;int,int&gt;&gt;&gt;, X then Y; absent/disabled means no preference.
        /// Selection never uses an item: the adopted caller attempts native UseItem exactly once, without a retry.
        /// </summary>
        public static ItemDrop.ItemData SelectSameFood(Player player, Inventory inventory, string sharedName)
        {
            if (player == null || player != Player.m_localPlayer || inventory == null
                || !ReferenceEquals(inventory, player.GetInventory()) || string.IsNullOrEmpty(sharedName))
                return null;
            if (!TryFoodCells(inventory, out var cells))
                return inventory.GetItem(sharedName);

            // Reuse the already adopted selection rule: preferred role first, native inventory order within each group.
            var items = inventory.GetAllItems();
            var candidates = new List<AutoEatCandidate>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                bool preferred = false;
                if (item != null)
                    foreach (var cell in cells)
                        if (item.m_gridPos.x == cell.Item1 && item.m_gridPos.y == cell.Item2)
                        { preferred = true; break; }
                // Preserve native Inventory.GetItem's current-world eligibility before applying a role preference.
                candidates.Add(new AutoEatCandidate(item != null && item.m_stack > 0 && item.m_worldLevel >= Game.m_worldLevel ? item.m_shared?.m_name : null,
                    preferred, i));
            }
            int selected = AutoEat.Select(sharedName, candidates);
            return selected < 0 ? null : items[selected];
        }

        internal static bool TryFoodCells(Inventory inventory, out IReadOnlyList<Tuple<int, int>> cells)
        {
            cells = null;
            if (!CruciblePlugin.IsReady || !CruciblePlugin.Session.AuthorityReady
                || CruciblePlugin.Session.WorldId == 0)
                return false;
            try
            {
                var registry = CruciblePlugin.Services.Capabilities;
                var providers = registry.Providers(CapabilityId);
                if (providers.Count != 1 || providers[0].Owner != ProviderId
                    || !registry.TryResolve<Func<IReadOnlyList<Tuple<int, int>>>>(CapabilityId, MinimumVersion, out var read))
                    return false;
                var published = read();
                if (published == null || published.Count == 0 || published.Count > 5)
                    return false;
                // A malformed/stale provider cannot establish a preferred role. Never infer roles from persisted data.
                var snapshot = new List<Tuple<int, int>>(published.Count);
                foreach (var cell in published)
                {
                    if (cell == null || cell.Item1 < 0 || cell.Item1 >= inventory.GetWidth()
                        || cell.Item2 < 0 || cell.Item2 >= inventory.GetHeight() || snapshot.Contains(cell))
                        return false;
                    snapshot.Add(cell);
                }
                cells = snapshot;
                return true;
            }
            catch
            {
                // Optional read-only integration failures retain the reference's ordinary inventory path.
                return false;
            }
        }
    }
}
