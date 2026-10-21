// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Puts a planned chest roll into a container's inventory (drops.md section 11): pre-rolled gear through
    /// <see cref="GearFactory.Build"/> (its state is in the item's custom data, which the container save carries), then
    /// the stones as stacks split at the stone's stack size. The inventory's own change callback saves the container on
    /// its owner. What does not fit is not added (a full chest loses the rest, as the game's own default items do;
    /// DECISIONS IMP-78). Owner only; main thread.
    /// </summary>
    internal static class ChestFiller
    {
        private static readonly Dictionary<StoneDef, int> Grouped = new Dictionary<StoneDef, int>();

        /// <summary>How much of a plan went in.</summary>
        public readonly struct Result
        {
            public Result(int gear, int stones, bool full)
            {
                Gear = gear;
                Stones = stones;
                Full = full;
            }

            public int Gear { get; }
            public int Stones { get; }
            public bool Full { get; }
        }

        public static Result Fill(Inventory inventory, LootPlan plan, bool cheated, RuleSet rules)
        {
            int gear = AddGear(inventory, plan, cheated, rules);
            int stones = AddStones(inventory, plan.Stones, cheated);
            return new Result(gear, stones, gear < plan.Gear.Count || stones < plan.Stones.Count);
        }

        private static int AddGear(Inventory inventory, LootPlan plan, bool cheated, RuleSet rules)
        {
            int added = 0;
            WeightedTable<GearBase> bases = plan.Gear.Count > 0 ? GearPool.ForTier(plan.Tier) : WeightedTable<GearBase>.Empty;
            foreach (RarityDef rarity in plan.Gear)
            {
                if (!inventory.HaveEmptySlot() || !bases.TryPick((float)LootRoller.Rng.NextDouble(), out GearBase gearBase))
                {
                    break;
                }
                ItemDrop.ItemData? item = GearFactory.Build(gearBase, rarity, cheated, LootRoller.Rng, rules);
                if (item != null && inventory.AddItem(item))
                {
                    added++;
                }
            }
            return added;
        }

        private static int AddStones(Inventory inventory, List<StoneDef> stones, bool cheated)
        {
            Grouped.Clear();
            foreach (StoneDef stone in stones)
            {
                Grouped.TryGetValue(stone, out int count);
                Grouped[stone] = count + 1;
            }
            int added = 0;
            foreach (KeyValuePair<StoneDef, int> pair in Grouped)
            {
                added += AddStone(inventory, pair.Key, pair.Value, cheated);
            }
            Grouped.Clear();
            return added;
        }

        // One stack per chunk of the stack size: the inventory's AddItem would put an oversized remainder in one slot.
        private static int AddStone(Inventory inventory, StoneDef stone, int amount, bool cheated)
        {
            int added = 0;
            for (int left = amount; left > 0;)
            {
                ItemDrop.ItemData? item = LootSpawner.StoneItem(stone, cheated);
                if (item == null)
                {
                    return added;
                }
                int chunk = Mathf.Min(left, Mathf.Max(1, item.m_shared.m_maxStackSize));
                item.m_stack = chunk;
                if (!inventory.CanAddItem(item) || !inventory.AddItem(item))
                {
                    return added;
                }
                added += chunk;
                left -= chunk;
            }
            return added;
        }
    }
}
